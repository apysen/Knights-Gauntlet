#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using KnightsGauntlet.Core;
using UnityEditor;
using UnityEngine;

public static class BattleMovementChecks
{
    [MenuItem("Tools/Knight's Gauntlet/Check Battle Movement")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        int turns = 0;
        foreach (var type in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
        {
            var ai = new EnemyAI(new System.Random(7));
            var pieces = new List<GamePiece>();
            ai.InitializePlans(pieces, 0);
            var arrival = new GamePiece(type, 2, 0) { Base = 0 };
            pieces.Add(arrival);
            ai.FilterNewSpawns(pieces, 0, 0);
            for (int tick = 0; tick < 3; tick++)
            {
                Require(ai.TryGetNextMove(arrival, out var target), type + " skipped turn " + tick);
                ai.MoveEnemies(pieces, -10, 100, 0);
                Require(arrival.Col == target.Col && arrival.Base == target.Row, "Arrival missed marker");
            }
        }

        // Repeated arrivals, captures and fractional scroll with a queen present.
        // Every displayed destination must execute exactly; neither saved buffer
        // may reroute an existing piece when a new row enters the board.
        for (int seed = 0; seed < 200; seed++)
        {
            var rng = new System.Random(seed);
            var ai = new EnemyAI(new System.Random(seed));
            var pieces = new List<GamePiece> { new GamePiece(PieceType.Queen, 2, 2) { Base = 2 } };
            ai.InitializePlans(pieces, 0);
            for (int tick = 0; tick < 25; tick++)
            {
                float scroll = tick * .37f;
                var before = SavedPlans(ai);
                int first = pieces.Count;
                pieces.Add(new GamePiece((PieceType)(1 + rng.Next(3)), rng.Next(5), -tick - 2 + scroll) { Base = -tick - 2 });
                ai.FilterNewSpawns(pieces, first, scroll);
                var after = SavedPlans(ai);
                foreach (var entry in before)
                    Require(after.ContainsKey(entry.Key) && after[entry.Key] == entry.Value, "Spawn changed saved move");
                var expected = new Dictionary<GamePiece, Int2>();
                foreach (var piece in pieces)
                    expected[piece] = ai.TryGetNextMove(piece, out var move) ? move : new Int2(piece.Col, (int)piece.Base);
                ai.MoveEnemies(pieces, -10, 100, scroll);
                var cells = new HashSet<string>();
                foreach (var piece in pieces)
                {
                    var target = expected[piece];
                    Require(piece.Col == target.Col && piece.Base == target.Row, "Move missed marker");
                    Require(Math.Abs(piece.Y - piece.Base - scroll) < .0001f, "Scroll drift");
                    Require(cells.Add(piece.Col + ":" + piece.Base), "Pieces overlapped");
                }
                if (tick % 4 == 3 && pieces.Count > 1) { pieces[1].Alive = false; pieces.RemoveAt(1); }
                turns++;
            }
        }
        return "PASS: all four arrival types move on their first three turns; " + turns +
            " mixed-board turns preserve both saved plans, execute markers exactly, and avoid overlap.";
    }

    private static Dictionary<string, string> SavedPlans(EnemyAI ai)
    {
        var result = new Dictionary<string, string>();
        foreach (string name in new[] { "_current", "_following" })
        {
            var plans = (System.Collections.IDictionary)typeof(EnemyAI).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ai);
            foreach (System.Collections.DictionaryEntry entry in plans)
            {
                if (!((GamePiece)entry.Key).Alive) continue;
                var move = entry.Value;
                var type = move.GetType();
                string value = "";
                foreach (string field in new[] { "FromCol", "FromRow", "Col", "Row", "Dir" })
                    value += type.GetField(field).GetValue(move) + ":";
                result.Add(name + ":" + ((GamePiece)entry.Key).GetHashCode(), value);
            }
        }
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
#endif
