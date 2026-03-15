using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tile-based A* pathfinder. Uses TileData.MovementCost for weighted movement.
/// Returns a list of tile positions from start (exclusive) to goal (inclusive).
/// Static utility — no MonoBehaviour overhead.
/// </summary>
public static class Pathfinder
{
    // Max tiles to examine per search (prevents frame stalls on impossible paths)
    private const int MAX_ITERATIONS = 4000;

    /// <summary>
    /// Find a path from start to goal. Returns null if no path found.
    /// </summary>
    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        bool allowDiagonal = false,
        bool ignoreImpassable = false)
    {
        var map = WorldMap.Instance;
        if (map == null) return null;

        var startTile = map.GetTile(start);
        var goalTile  = map.GetTile(goal);
        if (startTile == null || goalTile == null) return null;
        if (!ignoreImpassable && !goalTile.IsPassable) return null;

        var open   = new MinHeap<Node>();
        var closed = new HashSet<Vector2Int>();
        var nodeMap = new Dictionary<Vector2Int, Node>();

        var startNode = new Node(start, 0f, Heuristic(start, goal), null);
        open.Push(startNode);
        nodeMap[start] = startNode;

        int iter = 0;
        while (open.Count > 0 && iter++ < MAX_ITERATIONS)
        {
            var current = open.Pop();

            if (current.pos == goal)
                return ReconstructPath(current);

            closed.Add(current.pos);

            foreach (var neighbour in map.GetNeighbours(current.pos.x, current.pos.y, allowDiagonal))
            {
                if (closed.Contains(neighbour.Position)) continue;
                if (!ignoreImpassable && !neighbour.IsPassable) continue;

                float moveCost = neighbour.MovementCost;
                // Diagonal movement costs slightly more (Euclidean)
                if (allowDiagonal && neighbour.Position != current.pos)
                {
                    var delta = neighbour.Position - current.pos;
                    if (delta.x != 0 && delta.y != 0) moveCost *= 1.414f;
                }

                float gCost = current.gCost + moveCost;

                if (nodeMap.TryGetValue(neighbour.Position, out var existing))
                {
                    if (gCost < existing.gCost)
                    {
                        existing.gCost  = gCost;
                        existing.fCost  = gCost + existing.hCost;
                        existing.parent = current;
                        open.Push(existing); // re-push with better cost
                    }
                }
                else
                {
                    float h    = Heuristic(neighbour.Position, goal);
                    var newNode = new Node(neighbour.Position, gCost, h, current);
                    open.Push(newNode);
                    nodeMap[neighbour.Position] = newNode;
                }
            }
        }

        return null; // No path
    }

    // ── Heuristic: Octile distance (good for 8-dir, fine for 4-dir too) ──────

    static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx + dy + (1.414f - 2f) * Mathf.Min(dx, dy);
    }

    static List<Vector2Int> ReconstructPath(Node end)
    {
        var path = new List<Vector2Int>();
        var node = end;
        while (node.parent != null)
        {
            path.Add(node.pos);
            node = node.parent;
        }
        path.Reverse();
        return path;
    }

    // ── Node ─────────────────────────────────────────────────────────────────

    class Node : IHeapItem<Node>
    {
        public Vector2Int pos;
        public float gCost, hCost, fCost;
        public Node parent;
        public int HeapIndex { get; set; }

        public Node(Vector2Int pos, float g, float h, Node parent)
        {
            this.pos    = pos;
            this.gCost  = g;
            this.hCost  = h;
            this.fCost  = g + h;
            this.parent = parent;
        }

        public int CompareTo(Node other) => fCost.CompareTo(other.fCost);
    }
}

// ── MinHeap (binary heap for O(log n) A* open set) ───────────────────────────

public interface IHeapItem<T> : System.IComparable<T>
{
    int HeapIndex { get; set; }
}

public class MinHeap<T> where T : IHeapItem<T>
{
    private List<T> _items = new List<T>();
    public int Count => _items.Count;

    public void Push(T item)
    {
        item.HeapIndex = _items.Count;
        _items.Add(item);
        SiftUp(item.HeapIndex);
    }

    public T Pop()
    {
        T top = _items[0];
        int last = _items.Count - 1;
        _items[0] = _items[last];
        _items[0].HeapIndex = 0;
        _items.RemoveAt(last);
        if (_items.Count > 0) SiftDown(0);
        return top;
    }

    void SiftUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_items[i].CompareTo(_items[parent]) < 0)
            {
                Swap(i, parent);
                i = parent;
            }
            else break;
        }
    }

    void SiftDown(int i)
    {
        while (true)
        {
            int left = 2 * i + 1, right = 2 * i + 2, smallest = i;
            if (left  < _items.Count && _items[left ].CompareTo(_items[smallest]) < 0) smallest = left;
            if (right < _items.Count && _items[right].CompareTo(_items[smallest]) < 0) smallest = right;
            if (smallest == i) break;
            Swap(i, smallest);
            i = smallest;
        }
    }

    void Swap(int a, int b)
    {
        (_items[a], _items[b]) = (_items[b], _items[a]);
        _items[a].HeapIndex = a;
        _items[b].HeapIndex = b;
    }
}
