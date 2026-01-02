using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable UnusedMember.Global

/// <summary>
/// A lightweight Copy-On-Write wrapper for HashSet.
/// Provides immutable semantics: "modifying" the set returns a new instance,
/// leaving the original instance untouched.
/// </summary>
public readonly struct CowHashSet<T> : IEnumerable<T>, IEquatable<CowHashSet<T>>
    where T : IEquatable<T> {
  // The internal backing store.
  // If null, represents an empty set (avoids allocation for empty state).
  private readonly HashSet<T> _set;

  /// <summary>
  /// Returns a global empty instance.
  /// </summary>
  public static CowHashSet<T> Empty => default;

  /// <summary>
  /// Internal constructor to wrap an existing set.
  /// CAUTION: Only use this when you own the set and guarantee no one else will mutate it.
  /// </summary>
  private CowHashSet(HashSet<T> set) {
    _set = set;
  }

  /// <summary>
  /// Creates a new CowHashSet from an initial collection.
  /// </summary>
  public static CowHashSet<T> Create(IEnumerable<T> items) {
    return new CowHashSet<T>(new HashSet<T>(items));
  }

  // --- Core Properties ---

  public int Count => _set?.Count ?? 0;

  public bool Contains(T item) => _set != null && _set.Contains(item);

  // --- Mutation Methods (Copy-On-Write) ---

  /// <summary>
  /// Returns a NEW CowHashSet containing the added item.
  /// The original set is NOT modified.
  /// </summary>
  public CowHashSet<T> Add(T item) {
    // Optimization: If item already exists, return 'this' (no copy needed)
    if (_set != null && _set.Contains(item)) {
      return this;
    }

    // Copy
    var newSet = _set != null ? new HashSet<T>(_set) : new HashSet<T>();

    // Modify
    newSet.Add(item);

    // Return new wrapper
    return new CowHashSet<T>(newSet);
  }

  /// <summary>
  /// Returns a NEW CowHashSet with the item removed.
  /// The original set is NOT modified.
  /// </summary>
  public CowHashSet<T> Remove(T item) {
    if (_set == null || !_set.Contains(item)) {
      return this;
    }

    var newSet = new HashSet<T>(_set);
    newSet.Remove(item);
    return new CowHashSet<T>(newSet);
  }

  // --- Set Operations (Optional) ---

  public bool SetEquals(IEnumerable<T> other) {
    if (_set == null) {
      // If we are empty, we equal 'other' only if 'other' is also empty
      foreach (var _ in other) return false;
      return true;
    }

    return _set.SetEquals(other);
  }

  // --- Equality (Value Semantics) ---
  public bool Equals(CowHashSet<T> other) {
    // Null backing store (Empty) equals Null backing store
    if (_set == null) return other._set == null || other._set.Count == 0;
    if (other._set == null) return _set.Count == 0;

    // Fast pointer check
    if (ReferenceEquals(_set, other._set)) return true;

    return _set.SetEquals(other._set);
  }

  public override bool Equals(object obj) {
    return obj is CowHashSet<T> other && Equals(other);
  }

  public override int GetHashCode() {
    if (_set == null) return 0;

    // Simple aggregate hash for sets (order independent)
    int hash = 0;
    foreach (var item in _set) {
      hash ^= item.GetHashCode();
    }

    return hash;
  }

  public static bool operator ==(CowHashSet<T> left, CowHashSet<T> right) => left.Equals(right);
  public static bool operator !=(CowHashSet<T> left, CowHashSet<T> right) => !left.Equals(right);

  // --- Enumeration ---

  public IEnumerator<T> GetEnumerator() {
    return (_set ?? Enumerable.Empty<T>()).GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator() {
    return GetEnumerator();
  }
}
