using UnityEngine;

namespace KnightsGauntlet.View
{
    public partial class GameView
    {
        private int DrawLegacyThreat(int i, ThreatAgg agg)
        {
            var tc = GetThreat(i);
            tc.go.SetActive(true);
            bool onPlayer = (agg.Col == _pCol && agg.Row == _pRow);
            tc.go.transform.position = _board.CellToWorld(agg.Col, agg.Y);
            tc.go.transform.localScale = _board.CellScale();

            // Web palette: fill rgba(90,20,20,.55), straight outline rgba(150,40,40,.55).
            // Overlapping threats compound (as the web's alpha blend does) so a cell
            // threatened by two pieces reads a shade darker: 1 → .55, 2 → ~.80.
            tc.fillSR.enabled = true;
            tc.borderSR.enabled = true;

            float a = Mathf.Min(0.85f, 1f - Mathf.Pow(1f - 0.55f, agg.Count));
            tc.fillSR.color = new Color(0.353f, 0.078f, 0.078f, a);
            tc.borderSR.color = _colorblind ? new Color(0.95f, 0.32f, 0.32f, 1f)
                                            : new Color(0.588f, 0.157f, 0.157f, 0.55f);
            if (onPlayer)
            {
                // Pulse the outline on the knight's own square so an incoming hit reads.
                float pulse = 0.55f + 0.45f * Mathf.PingPong(Time.time * 3f, 1f);
                tc.borderSR.color = new Color(1f, 0.45f, 0.45f, pulse);
            }
            return i + 1;
        }
    }
}
