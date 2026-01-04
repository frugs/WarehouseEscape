using System;
using System.Collections.Generic;

public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority> {
    private readonly List<(TElement Element, TPriority Priority)> _data = new();

    public int Count => _data.Count;

    public void Enqueue(TElement element, TPriority priority) {
        _data.Add((element, priority));
        int ci = _data.Count - 1; // child index
        while (ci > 0) {
            int pi = (ci - 1) / 2; // parent index
            if (_data[ci].Priority.CompareTo(_data[pi].Priority) >= 0) break;
            (_data[ci], _data[pi]) = (_data[pi], _data[ci]);
            ci = pi;
        }
    }

    public TElement Dequeue() {
        int li = _data.Count - 1;
        TElement frontItem = _data[0].Element;
        _data[0] = _data[li];
        _data.RemoveAt(li);

        --li;
        int pi = 0;
        while (true) {
            int ci = pi * 2 + 1;
            if (ci > li) break;
            int rc = ci + 1;
            if (rc <= li && _data[rc].Priority.CompareTo(_data[ci].Priority) < 0) ci = rc;
            if (_data[pi].Priority.CompareTo(_data[ci].Priority) <= 0) break;
            (_data[pi], _data[ci]) = (_data[ci], _data[pi]);
            pi = ci;
        }
        return frontItem;
    }
}
