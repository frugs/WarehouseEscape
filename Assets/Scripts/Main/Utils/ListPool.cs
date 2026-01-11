using System.Collections.Generic;

/// <summary>
/// A non-static, instance-based list pool.
/// Instantiate one per thread to ensure thread safety without locks.
/// </summary>
public class ListPool<T>
{
    // Local stack for this specific pool instance
    private readonly Stack<List<T>> _pool = new Stack<List<T>>();

    /// <summary>
    /// Gets a list from this specific pool instance.
    /// </summary>
    public List<T> Get()
    {
        if (_pool.Count > 0)
        {
            return _pool.Pop();
        }

        return new List<T>();
    }

    /// <summary>
    /// Returns the list to this specific pool instance.
    /// </summary>
    public void Release(List<T> list)
    {
        list.Clear(); // Ensure list is clean for next use [web:31]
        _pool.Push(list);
    }
}
