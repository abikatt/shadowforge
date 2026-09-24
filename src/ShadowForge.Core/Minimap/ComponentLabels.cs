namespace ShadowForge.Minimap;

/// <summary>
/// 4-connected components of a mask. Labels start at 1, 0 marks unset
/// pixels, and <see cref="Sizes"/> is indexed by label minus one.
/// </summary>
internal sealed class ComponentLabels
{
    public int[] Labels { get; }
    public List<int> Sizes { get; } = new();

    /// <summary>
    /// <paramref name="connected"/>, when given, decides whether a step from
    /// one set pixel index to a neighboring set pixel index joins them.
    /// </summary>
    public ComponentLabels(bool[] mask, int width, int height, Func<int, int, bool>? connected = null)
    {
        Labels = new int[mask.Length];
        var stack = new Stack<int>();

        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || Labels[start] != 0) continue;

            int label = Sizes.Count + 1;
            int size = 0;
            Labels[start] = label;
            stack.Push(start);

            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                size++;
                int x = idx % width;
                int y = idx / width;

                if (x > 0) TryPush(idx, idx - 1);
                if (x < width - 1) TryPush(idx, idx + 1);
                if (y > 0) TryPush(idx, idx - width);
                if (y < height - 1) TryPush(idx, idx + width);
            }

            Sizes.Add(size);

            void TryPush(int from, int to)
            {
                if (!mask[to] || Labels[to] != 0) return;
                if (connected is not null && !connected(from, to)) return;
                Labels[to] = label;
                stack.Push(to);
            }
        }
    }

    /// <summary>
    /// Per label, whether the component has at least
    /// <paramref name="fracOfLargest"/> times the largest component's size.
    /// Index 0 (unset pixels) is always false.
    /// </summary>
    public bool[] KeepAtLeast(float fracOfLargest)
    {
        var keep = new bool[Sizes.Count + 1];
        if (Sizes.Count == 0) return keep;

        float threshold = fracOfLargest * Sizes.Max();
        for (int i = 0; i < Sizes.Count; i++)
            keep[i + 1] = Sizes[i] >= threshold;
        return keep;
    }
}
