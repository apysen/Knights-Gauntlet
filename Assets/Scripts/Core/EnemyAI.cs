using System;
using System.Collections.Generic;

namespace KnightsGauntlet.Core
{
    // Battle movement is committed one cycle ahead. Execution never makes random
    // choices or resolves occupancy; both have already been settled by planning.
    public class EnemyAI
    {
        private readonly Random _rng;
        private Dictionary<GamePiece, PlannedMove> _current = new Dictionary<GamePiece, PlannedMove>();
        private Dictionary<GamePiece, PlannedMove> _following = new Dictionary<GamePiece, PlannedMove>();

        public EnemyAI(Random rng) { _rng = rng; }

        private struct PlannedMove
        {
            public int FromCol, FromRow, Col, Row, Dir;
            public bool Moves => Col != FromCol || Row != FromRow;
        }

        private struct PlanningPiece
        {
            public GamePiece Piece;
            public int Col, Row, Dir;
        }

        private struct Option
        {
            public int Cell, Rank;
        }

        private static int BaseRow(GamePiece piece, float scroll)
            => (int)Math.Round(float.IsNaN(piece.Base) ? piece.Y - scroll : piece.Base);

        private static long Key(int col, int row) => ((long)col << 32) | (uint)row;

        public void ClearPlans()
        {
            _current.Clear();
            _following.Clear();
        }

        public void InitializePlans(IList<GamePiece> pieces, float scroll)
        {
            var board = Snapshot(pieces, scroll);
            _current = BuildPlan(board);
            _following = BuildPlan(Predict(board, _current));
            PublishOffsets(pieces, scroll);
        }

        // Returned coordinates are scroll-invariant board cells. Staying put has
        // no movement marker. Queens also have their separate adjacent attack area.
        public bool TryGetNextMove(GamePiece piece, out Int2 destination)
        {
            if (piece.Alive && _current.TryGetValue(piece, out var move) && move.Moves)
            {
                destination = new Int2(move.Col, move.Row);
                return true;
            }
            destination = default;
            return false;
        }

        public bool MoveEnemies(IList<GamePiece> pieces, int playerCol, float playerY, float scroll)
        {
            bool dead = false;
            // Preserve the queen's existing adjacent strike before she travels.
            foreach (var piece in pieces)
            {
                if (!piece.Alive || piece.Type != PieceType.Queen) continue;
                foreach (var step in GameConfig.QueenAdj)
                    if (piece.Col + step.Col == playerCol &&
                        Math.Abs(piece.Y + step.Row - playerY) < 0.5f) dead = true;
            }

            foreach (var piece in pieces)
            {
                if (!piece.Alive || !_current.TryGetValue(piece, out var move)) continue;
                piece.Col = move.Col;
                piece.Base = move.Row;
                piece.Y = move.Row + scroll;
                piece.Dir = move.Dir;
                if (piece.Type != PieceType.Trap && piece.Col == playerCol &&
                    Math.Abs(piece.Y - playerY) < 0.5f) dead = true;
            }

            // Promote the already finished buffer. Captured/culled pieces are
            // simply absent; surviving pieces keep their exact committed moves.
            var board = Snapshot(pieces, scroll);
            var promoted = new Dictionary<GamePiece, PlannedMove>();
            foreach (var source in board)
            {
                if (_following.TryGetValue(source.Piece, out var move))
                    promoted.Add(source.Piece, move);
                else
                    promoted.Add(source.Piece, Stay(source));
            }
            _current = promoted;
            PublishOffsets(pieces, scroll);
            // Work on the plan beyond the one now displayed. Its starting board
            // is predicted after the displayed plan executes.
            _following = BuildPlan(Predict(board, _current));
            return dead;
        }

        // Do not spawn onto cells already reserved by either saved plan: that
        // would force a reroute. Enrol accepted arrivals immediately so they do
        // not lose two movement turns waiting to enter the planning buffers.
        public void FilterNewSpawns(List<GamePiece> pieces, int firstNewPiece, float scroll)
        {
            for (int i = pieces.Count - 1; i >= firstNewPiece; i--)
            {
                var incoming = pieces[i];
                int row = BaseRow(incoming, scroll);
                bool blocked = false;
                for (int j = 0; j < firstNewPiece; j++)
                {
                    var other = pieces[j];
                    if (!other.Alive) continue;
                    if ((other.Col == incoming.Col && BaseRow(other, scroll) == row) ||
                        (_current.TryGetValue(other, out var current) &&
                            current.Col == incoming.Col && current.Row == row) ||
                        (_following.TryGetValue(other, out var following) &&
                            following.Col == incoming.Col && following.Row == row))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) pieces.RemoveAt(i);
            }
            if (pieces.Count <= firstNewPiece) return;
            var board = Snapshot(pieces, scroll);
            var futureReservations = new HashSet<long>();
            foreach (var source in board)
                if (_following.TryGetValue(source.Piece, out var move))
                    futureReservations.Add(Key(move.Col, move.Row));
            _current = BuildPlan(board, _current, futureReservations);
            _following = BuildPlan(Predict(board, _current), _following);
            PublishOffsets(pieces, scroll);
        }

        private static PlannedMove Stay(PlanningPiece source) => new PlannedMove
        {
            FromCol = source.Col, FromRow = source.Row,
            Col = source.Col, Row = source.Row, Dir = source.Dir
        };

        private static List<PlanningPiece> Snapshot(IList<GamePiece> pieces, float scroll)
        {
            var board = new List<PlanningPiece>();
            foreach (var piece in pieces)
                if (piece.Alive) board.Add(new PlanningPiece
                {
                    Piece = piece, Col = piece.Col, Row = BaseRow(piece, scroll), Dir = piece.Dir
                });
            return board;
        }

        private static List<PlanningPiece> Predict(List<PlanningPiece> board,
            Dictionary<GamePiece, PlannedMove> plan)
        {
            var result = new List<PlanningPiece>(board.Count);
            foreach (var source in board)
            {
                var predicted = source;
                if (plan.TryGetValue(source.Piece, out var move))
                {
                    predicted.Col = move.Col;
                    predicted.Row = move.Row;
                    predicted.Dir = move.Dir;
                }
                result.Add(predicted);
            }
            return result;
        }

        private void PublishOffsets(IList<GamePiece> pieces, float scroll)
        {
            // Keep the original public offset fields useful for existing callers.
            foreach (var piece in pieces)
            {
                piece.PlanDC = null;
                piece.PlanDR = null;
                if (TryGetNextMove(piece, out var destination))
                {
                    piece.PlanDC = destination.Col - piece.Col;
                    piece.PlanDR = destination.Row - BaseRow(piece, scroll);
                }
            }
        }

        private Dictionary<GamePiece, PlannedMove> BuildPlan(List<PlanningPiece> board,
            Dictionary<GamePiece, PlannedMove> committed = null, HashSet<long> futureReservations = null)
        {
            var plan = new Dictionary<GamePiece, PlannedMove>();
            int count = board.Count;
            if (count == 0) return plan;
            var cells = new List<Int2>();
            var cellIndices = new Dictionary<long, int>();
            int Cell(int col, int row)
            {
                long key = Key(col, row);
                if (!cellIndices.TryGetValue(key, out int index))
                {
                    index = cells.Count;
                    cells.Add(new Int2(col, row));
                    cellIndices.Add(key, index);
                }
                return index;
            }
            // Every piece can hold its own square, so a complete assignment
            // always exists, including stationary pawns and traps.
            foreach (var source in board) Cell(source.Col, source.Row);
            if (committed != null)
                foreach (var source in board)
                    if (committed.TryGetValue(source.Piece, out var saved)) Cell(saved.Col, saved.Row);
            var options = new List<Option>[count];
            for (int i = 0; i < count; i++)
            {
                var source = board[i];
                var choices = options[i] = new List<Option>();
                void Add(int dc, int dr, int rank)
                {
                    int col = source.Col + dc;
                    // An arrival must be able to hold this cell on the following
                    // turn if necessary, without displacing a saved destination.
                    if (col >= 0 && col < GameConfig.Cols &&
                        (futureReservations == null || !futureReservations.Contains(Key(col, source.Row + dr))))
                        choices.Add(new Option { Cell = Cell(col, source.Row + dr), Rank = rank });
                }
                switch (source.Piece.Type)
                {
                    case PieceType.Rook:
                        Add(0, 1, 0);
                        Add(-1, 0, 1);
                        Add(1, 0, 1);
                        break;
                    case PieceType.Bishop:
                        int dir = source.Dir != 0 ? source.Dir : 1;
                        Add(dir, 1, 0);
                        Add(-dir, 1, 1);
                        break;
                    case PieceType.Knight:
                        foreach (var step in GameConfig.KnightJumps)
                            Add(step.Col, step.Row, step.Row > 0 ? 0 : 1);
                        break;
                    case PieceType.Queen:
                        foreach (var step in GameConfig.QueenAdj)
                            Add(step.Col, step.Row, step.Row >= 0 ? 0 : 1);
                        break;
                }
            }

            // Minimum-cost assignment maximizes how many pieces move first,
            // then prefers normal directions. Saved random costs break ties.
            // Occupied destinations are legal only if their occupants get other
            // destinations in the same assignment (chains and swaps work too).
            long stayCost = (count + 1L) * 4096L;
            long illegalCost = stayCost * (count + 1L) * 4L;
            var costs = new long[count, cells.Count];
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < cells.Count; j++) costs[i, j] = illegalCost;
                if (committed != null && committed.TryGetValue(board[i].Piece, out var saved))
                {
                    costs[i, Cell(saved.Col, saved.Row)] = 0;
                    continue;
                }
                costs[i, Cell(board[i].Col, board[i].Row)] = stayCost + _rng.Next(128);
                foreach (var option in options[i])
                    costs[i, option.Cell] = option.Rank * 128L + _rng.Next(128);
            }
            int[] assigned = Assign(costs);
            for (int i = 0; i < count; i++)
            {
                var source = board[i];
                if (committed != null && committed.TryGetValue(source.Piece, out var savedMove))
                {
                    plan.Add(source.Piece, savedMove);
                    continue;
                }
                var destination = cells[assigned[i]];
                var move = Stay(source);
                move.Col = destination.Col;
                move.Row = destination.Row;
                if (source.Piece.Type == PieceType.Bishop && move.Moves)
                    move.Dir = Math.Sign(move.Col - source.Col);
                plan.Add(source.Piece, move);
            }
            return plan;
        }

        // Rectangular Hungarian assignment. There is one row per piece and one
        // column per reachable cell; each cell can be assigned to only one piece.
        private static int[] Assign(long[,] costs)
        {
            int rows = costs.GetLength(0), cols = costs.GetLength(1);
            var rowPotential = new long[rows + 1];
            var colPotential = new long[cols + 1];
            var owner = new int[cols + 1];
            var previous = new int[cols + 1];
            const long infinity = long.MaxValue / 4;
            for (int row = 1; row <= rows; row++)
            {
                owner[0] = row;
                int column = 0;
                var minimum = new long[cols + 1];
                var used = new bool[cols + 1];
                for (int j = 1; j <= cols; j++) minimum[j] = infinity;
                do
                {
                    used[column] = true;
                    int activeRow = owner[column], next = 0;
                    long delta = infinity;
                    for (int j = 1; j <= cols; j++)
                    {
                        if (used[j]) continue;
                        long cost = costs[activeRow - 1, j - 1] - rowPotential[activeRow] - colPotential[j];
                        if (cost < minimum[j]) { minimum[j] = cost; previous[j] = column; }
                        if (minimum[j] < delta) { delta = minimum[j]; next = j; }
                    }
                    for (int j = 0; j <= cols; j++)
                    {
                        if (used[j]) { rowPotential[owner[j]] += delta; colPotential[j] -= delta; }
                        else minimum[j] -= delta;
                    }
                    column = next;
                } while (owner[column] != 0);
                do
                {
                    int next = previous[column];
                    owner[column] = owner[next];
                    column = next;
                } while (column != 0);
            }
            var assigned = new int[rows];
            for (int j = 1; j <= cols; j++)
                if (owner[j] != 0) assigned[owner[j] - 1] = j - 1;
            return assigned;
        }
    }
}
