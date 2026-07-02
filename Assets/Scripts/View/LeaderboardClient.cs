using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using KnightsGauntlet.Core;

namespace KnightsGauntlet.View
{
    // One leaderboard row.
    [Serializable]
    public class ScoreRow
    {
        public string name;
        public int score;
        public string skin;
    }

    // Supabase REST client (replaces Game Center). Point supabaseUrl/Key at the
    // SAME project your web build uses and every platform shares one board.
    // The anon/publishable key is meant to be public; data is protected by Row
    // Level Security on Supabase, not by hiding the key.
    public class LeaderboardClient : MonoBehaviour
    {
        public string supabaseUrl;
        public string supabaseKey;

        [Serializable] private class RowList { public ScoreRow[] items; }
        [Serializable] private class LocalList { public ScoreRow[] items; }
        [Serializable] private class PostBody { public string name; public int score; public string category; public string skin; }
        [Serializable] private class PostBodyNoSkin { public string name; public int score; public string category; }
        [Serializable] private class SkinBody { public string skin; }

        public static bool IsOnlineCategory(string category)
            => category != null && (category.EndsWith("hard") || category.EndsWith("grandmaster")
                                    || category.StartsWith("daily"));

        // Is this name free to claim? Queries the scores table; on no-config or a
        // network error we allow it (can't verify offline), so play isn't blocked.
        public void CheckNameAvailable(string name, Action<bool> done)
        {
            // The developer's handle is always allowed (reclaimable across resets).
            if (!string.IsNullOrEmpty(name) && name.ToLower() == "apy") { done?.Invoke(true); return; }
            if (!Configured() || string.IsNullOrEmpty(name)) { done?.Invoke(true); return; }
            StartCoroutine(CheckCo(name, done));
        }

        private IEnumerator CheckCo(string name, Action<bool> done)
        {
            string url = supabaseUrl + "/rest/v1/scores?select=name&limit=1&name=eq." +
                         UnityWebRequest.EscapeURL(name);
            bool available = true;
            yield return Get(url,
                text => { var rows = Parse(text); available = (rows == null || rows.Length == 0); },
                () => { available = true; });
            done?.Invoke(available);
        }

        private void SetHeaders(UnityWebRequest req, bool json)
        {
            req.SetRequestHeader("apikey", supabaseKey);
            req.SetRequestHeader("Authorization", "Bearer " + supabaseKey);
            if (json) req.SetRequestHeader("Content-Type", "application/json");
        }

        // ── Submit ───────────────────────────────────────────────────────────
        public void SubmitScore(string category, string name, int score, string skin)
        {
            SaveLocal(category, name, score, skin);
            if (IsOnlineCategory(category) && Configured())
                StartCoroutine(PostCo(category, name, score, skin));
        }

        private IEnumerator PostCo(string category, string name, int score, string skin)
        {
            string url = supabaseUrl + "/rest/v1/scores";
            string body = JsonUtility.ToJson(new PostBody { name = name, score = score, category = category, skin = skin });
            yield return Send(url, "POST", body, null, () =>
            {
                // skin column may not exist yet → retry without it
                string b2 = JsonUtility.ToJson(new PostBodyNoSkin { name = name, score = score, category = category });
                StartCoroutine(Send(url, "POST", b2, null, null));
            });
        }

        // ── Fetch online ─────────────────────────────────────────────────────
        public void FetchOnline(string category, Action<List<ScoreRow>> done)
        {
            if (!Configured()) { done?.Invoke(new List<ScoreRow>()); return; }
            StartCoroutine(FetchCo(category, done));
        }

        private IEnumerator FetchCo(string category, Action<List<ScoreRow>> done)
        {
            string baseUrl = supabaseUrl + "/rest/v1/scores?category=eq." +
                             UnityWebRequest.EscapeURL(category) + "&order=score.desc&limit=1000";
            List<ScoreRow> result = null;

            yield return Get(baseUrl + "&select=name,score,skin",
                text => result = Dedup(Parse(text)),
                () => { /* fall through to retry without skin */ });

            if (result == null)
                yield return Get(baseUrl + "&select=name,score",
                    text => result = Dedup(Parse(text)), null);

            done?.Invoke(result ?? new List<ScoreRow>());
        }

        private static List<ScoreRow> Dedup(ScoreRow[] rows)
        {
            var seen = new HashSet<string>();
            var outList = new List<ScoreRow>();
            foreach (var r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.name) || seen.Contains(r.name)) continue;
                seen.Add(r.name);
                outList.Add(r);
                if (outList.Count >= 500) break;
            }
            return outList;
        }

        // ── Sync equipped skin onto the player's rows ────────────────────────
        public void SyncEquippedSkin(string name, string skin)
        {
            if (!Configured() || string.IsNullOrEmpty(name) || !NameFilter.IsAllowed(name)) return;
            string url = supabaseUrl + "/rest/v1/scores?name=eq." + UnityWebRequest.EscapeURL(name);
            StartCoroutine(Send(url, "PATCH", JsonUtility.ToJson(new SkinBody { skin = skin }), null, null));
        }

        // ── Local (personal) scores ──────────────────────────────────────────
        public List<ScoreRow> GetLocal(string category)
        {
            var json = PlayerPrefs.GetString("kg_local_" + category, "");
            if (string.IsNullOrEmpty(json)) return new List<ScoreRow>();
            try
            {
                var wrapped = JsonUtility.FromJson<LocalList>(json);
                return wrapped?.items != null ? new List<ScoreRow>(wrapped.items) : new List<ScoreRow>();
            }
            catch { return new List<ScoreRow>(); }
        }

        private void SaveLocal(string category, string name, int score, string skin)
        {
            var list = GetLocal(category);
            list.Add(new ScoreRow { name = name, score = score, skin = skin });
            list.Sort((a, b) => b.score.CompareTo(a.score));
            if (list.Count > 10) list = list.GetRange(0, 10);
            PlayerPrefs.SetString("kg_local_" + category,
                JsonUtility.ToJson(new LocalList { items = list.ToArray() }));
            PlayerPrefs.Save();
        }

        // ── HTTP helpers ─────────────────────────────────────────────────────
        private bool Configured() => !string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey);

        private static ScoreRow[] Parse(string arrayJson)
        {
            try
            {
                var wrapped = "{\"items\":" + arrayJson + "}";
                var list = JsonUtility.FromJson<RowList>(wrapped);
                return list?.items ?? new ScoreRow[0];
            }
            catch { return new ScoreRow[0]; }
        }

        private IEnumerator Get(string url, Action<string> onOk, Action onFail)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                SetHeaders(req, false);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(req.downloadHandler.text);
                else onFail?.Invoke();
            }
        }

        private IEnumerator Send(string url, string method, string body, Action onOk, Action onFail)
        {
            using (var req = new UnityWebRequest(url, method))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                SetHeaders(req, true);
                req.SetRequestHeader("Prefer", "return=minimal");
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke();
                else onFail?.Invoke();
            }
        }
    }
}
