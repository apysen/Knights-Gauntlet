using UnityEngine;
using TMPro;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // Floating text: +score / xCombo / shielded! / coin values.
    //
    // In the 3D board the pieces stand up toward the tilted camera, so text spawned on
    // the board plane gets covered by the piece on that square. Player popups therefore
    // anchor above the player's head (measured from the live model bounds, so every
    // piece and skin works), sit a little toward the camera so depth never hides them,
    // and face the camera. Each one pops in with an overshoot, rises on an ease-out with
    // a small sideways drift and wobble, flashes white into its color, then shrinks out.
    public partial class GameView
    {
        private const float PopupLife = 0.95f;   // seconds, after any delay
        private const float PopupPopIn = 0.20f;  // overshoot pop-in
        private const float PopupFlash = 0.14f;  // white flash fading into the color
        private static readonly Color PopupOutline = new Color32(0x14, 0x0E, 0x10, 0xFF);

        // Board-plane popup (coins).
        public void SpawnPopup(int col, float y, string text, Color color)
        {
            if (_board == null) return;
            Vector3 pos = _board.CellToWorld(col, y);
            pos = TowardCamera(pos, _board.cellSize * 0.35f);
            Launch(pos, text, color, 1f, 0f);
        }

        // Popup stacked above the player piece. line 0 sits just over the head, each
        // further line one text row higher. scale > 1 for punchier lines (big combos).
        public void SpawnPlayerPopup(GameSimulation sim, int line, string text, Color color,
                                     float scale = 1f, float delay = 0f)
        {
            if (_board == null || sim == null) return;
            float cell = _board.cellSize;
            var cam = Camera.main;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            Vector3 cellPos = _board.CellToWorld(sim.PlayerCol, sim.PlayerY);
            Vector3 pos = cellPos + up * (PlayerScreenHeight(up, cell) + cell * (0.22f + line * 0.56f));
            Launch(TowardCamera(pos, cell * 0.6f), text, color, scale, delay);
        }

        // How far the player piece reaches up the screen from its own origin, in world
        // units along the camera's up axis. Uses the raw mesh bounds: the renderers'
        // bounds are inflated on purpose (the board roll shader moves vertices), so they
        // can't be measured. Sprites (the pixel knight skin) use their real bounds.
        private float PlayerScreenHeight(Vector3 up, float cell)
        {
            if (_player == null) return cell;
            Vector3 origin = _player.transform.position;
            float best = float.NegativeInfinity;
            foreach (var mf in _player.GetComponentsInChildren<MeshFilter>(false))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                             (i & 2) == 0 ? b.min.y : b.max.y,
                                             (i & 4) == 0 ? b.min.z : b.max.z);
                    best = Mathf.Max(best, Vector3.Dot(mf.transform.TransformPoint(corner) - origin, up));
                }
            }
            foreach (var sr in _player.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (!sr.enabled || sr.sprite == null || sr.gameObject == _player.gameObject) continue;
                var b = sr.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                             (i & 2) == 0 ? b.min.y : b.max.y,
                                             (i & 4) == 0 ? b.min.z : b.max.z);
                    best = Mathf.Max(best, Vector3.Dot(corner - origin, up));
                }
            }
            return float.IsInfinity(best) ? cell : Mathf.Clamp(best, cell * 0.3f, cell * 2.2f);
        }

        private static Vector3 TowardCamera(Vector3 pos, float amount)
        {
            var cam = Camera.main;
            return cam != null ? pos - cam.transform.forward * amount : pos + Vector3.back * amount;
        }

        private void Launch(Vector3 pos, string text, Color color, float scale, float delay)
        {
            var item = GetPopup();
            item.active = true;
            item.age = -Mathf.Max(0f, delay);
            item.color = color;
            item.scale = scale;
            item.origin = pos;
            // Small random sideways drift and lean so stacked captures don't line up.
            item.drift = Random.Range(-0.18f, 0.18f);
            item.lean = Random.Range(-7f, 7f);
            item.tmp.text = text;
            item.tmp.color = Color.white;
            item.tmp.transform.position = pos;
            item.tmp.transform.localScale = Vector3.zero;
            item.tmp.gameObject.SetActive(delay <= 0f);
            OrientPopup(item, 0f);
        }

        private float _popupLastPlayerY = float.NaN;

        private void AnimatePopups(GameSimulation sim, float dt)
        {
            // Ride the board scroll: in Battle the player's row drifts continuously as the
            // board advances, so popups follow that drift and stay with the piece. Whole-row
            // jumps are moves, not scroll, and are ignored.
            float scroll = 0f;
            if (sim != null)
            {
                float dy = sim.PlayerY - _popupLastPlayerY;
                if (!float.IsNaN(dy) && Mathf.Abs(dy) < 0.3f) scroll = dy;
                _popupLastPlayerY = sim.PlayerY;
            }
            if (_popups.Count == 0) return;
            float cell = _board != null ? _board.cellSize : 1f;
            Vector3 scrollShift = new Vector3(0f, -scroll * cell, 0f); // CellToWorld: y falls as simY grows
            var cam = Camera.main;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            for (int i = 0; i < _popups.Count; i++)
            {
                var it = _popups[i];
                if (!it.active) continue;
                it.age += dt;
                it.origin += scrollShift;
                if (it.age < 0f) continue; // still waiting on its stagger delay
                if (!it.tmp.gameObject.activeSelf) it.tmp.gameObject.SetActive(true);
                if (it.age >= PopupLife) { it.active = false; it.tmp.gameObject.SetActive(false); continue; }

                float t = it.age / PopupLife;
                // Rise fast then settle (ease-out cubic), drifting a touch sideways.
                float rise = 1f - Mathf.Pow(1f - t, 3f);
                it.tmp.transform.position = it.origin + up * (rise * cell * 0.75f) + right * (rise * it.drift * cell);

                // Scale: overshoot pop-in, hold, then shrink away in the last 25%.
                float s;
                if (it.age < PopupPopIn) s = PopupEaseOutBack(it.age / PopupPopIn);
                else s = 1f;
                float outT = Mathf.InverseLerp(0.75f, 1f, t);
                s *= 1f - outT * 0.45f;
                it.tmp.transform.localScale = Vector3.one * (cell * it.scale * s);

                // White flash into the color, fading out with the shrink.
                Color c = Color.Lerp(Color.white, it.color, Mathf.Clamp01(it.age / PopupFlash));
                c.a = 1f - outT;
                it.tmp.color = c;

                OrientPopup(it, t);
            }
        }

        // Face the camera in 3D, with a lean that wobbles and settles.
        private void OrientPopup(PopupItem it, float t)
        {
            float wobble = it.lean * Mathf.Cos(t * 18f) * (1f - t);
            var cam = Camera.main;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            it.tmp.transform.rotation = face * Quaternion.Euler(0f, 0f, wobble);
        }

        private static float PopupEaseOutBack(float x)
        {
            const float c1 = 2.2f, c3 = c1 + 1f;
            x = Mathf.Clamp01(x) - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private PopupItem GetPopup()
        {
            for (int i = 0; i < _popups.Count; i++) if (!_popups[i].active) return _popups[i];
            var go = new GameObject("Popup");
            go.transform.SetParent(_popupsRoot, false);
            go.transform.localScale = Vector3.one * _board.cellSize;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Normal;
            // Keep score lettering compact enough to leave the next move visible.
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 1; tmp.fontSizeMax = 4.5f;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 1f);
            if (_theme != null && _theme.popupFont != null) tmp.font = _theme.popupFont;
            else if (_theme != null && _theme.uiFont != null) tmp.font = _theme.uiFont;
            // Dark outline so the text reads over gold pieces and bright tiles.
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = PopupOutline;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 20;
            var item = new PopupItem { tmp = tmp };
            _popups.Add(item);
            return item;
        }

        private class PopupItem
        {
            public TextMeshPro tmp;
            public bool active;
            public float age, scale, drift, lean;
            public Color color;
            public Vector3 origin;
        }
    }
}
