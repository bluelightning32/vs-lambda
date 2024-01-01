using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace LambdaFactory;

class ListEqualityComparer<T> : IEqualityComparer<List<T>> {
  public bool Equals(List<T> x, List<T> y) {
    if (x == null) {
      return y == null;
    }
    if (y == null) {
      return false;
    }
    return x.SequenceEqual<T>(y);
  }

  public int GetHashCode([DisallowNull] List<T> list) {
    HashCode code = new HashCode();
    foreach (T t in list) {
      code.Add(t);
    }
    return code.ToHashCode();
  }

  public static string GetString(List<T> list) {
    StringBuilder result = new StringBuilder();
    foreach (T t in list) {
      result.Append(t.ToString());
      result.Append(", ");
    }
    return result.ToString();
  }
}