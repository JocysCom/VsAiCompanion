using System;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public interface IDataConnectionProperties
  {
    void Reset();

    void Parse(string s);

    bool IsExtensible { get; }

    void Add(string propertyName);

    bool Contains(string propertyName);

    object this[string propertyName] { get; set; }

    void Remove(string propertyName);

    event EventHandler PropertyChanged;

    void Reset(string propertyName);

    bool IsComplete { get; }

    void Test();

    string ToFullString();

    string ToDisplayString();
  }
}
