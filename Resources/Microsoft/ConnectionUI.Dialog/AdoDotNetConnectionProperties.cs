using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class AdoDotNetConnectionProperties : IDataConnectionProperties, ICustomTypeDescriptor
  {
    private string _providerName;
    private DbConnectionStringBuilder _connectionStringBuilder;

    public AdoDotNetConnectionProperties(string providerName)
    {
      this._providerName = providerName;
      this._connectionStringBuilder = DbProviderFactories.GetFactory(providerName).CreateConnectionStringBuilder();
      this._connectionStringBuilder.BrowsableConnectionString = false;
    }

    public virtual void Reset()
    {
      this._connectionStringBuilder.Clear();
      this.OnPropertyChanged(EventArgs.Empty);
    }

    public virtual void Parse(string s)
    {
      this._connectionStringBuilder.ConnectionString = s;
      this.OnPropertyChanged(EventArgs.Empty);
    }

    public virtual bool IsExtensible => !this._connectionStringBuilder.IsFixedSize;

    public virtual void Add(string propertyName)
    {
      if (this._connectionStringBuilder.ContainsKey(propertyName))
        return;
      this._connectionStringBuilder.Add(propertyName, (object) string.Empty);
      this.OnPropertyChanged(EventArgs.Empty);
    }

    public virtual bool Contains(string propertyName)
    {
      return this._connectionStringBuilder.ContainsKey(propertyName);
    }

    public virtual object this[string propertyName]
    {
      get
      {
        if (propertyName == null)
          throw new ArgumentNullException(nameof (propertyName));
        object obj = (object) null;
        if (!this._connectionStringBuilder.TryGetValue(propertyName, out obj))
          return (object) null;
        return this._connectionStringBuilder.ShouldSerialize(propertyName) ? this._connectionStringBuilder[propertyName] : this._connectionStringBuilder[propertyName] ?? (object) DBNull.Value;
      }
      set
      {
        if (propertyName == null)
          throw new ArgumentNullException(nameof (propertyName));
        this._connectionStringBuilder.Remove(propertyName);
        if (value == DBNull.Value)
        {
          this.OnPropertyChanged(EventArgs.Empty);
        }
        else
        {
          object objA = (object) null;
          this._connectionStringBuilder.TryGetValue(propertyName, out objA);
          this._connectionStringBuilder[propertyName] = value;
          if (object.Equals(objA, value))
            this._connectionStringBuilder.Remove(propertyName);
          this.OnPropertyChanged(EventArgs.Empty);
        }
      }
    }

    public virtual void Remove(string propertyName)
    {
      if (!this._connectionStringBuilder.ContainsKey(propertyName))
        return;
      this._connectionStringBuilder.Remove(propertyName);
      this.OnPropertyChanged(EventArgs.Empty);
    }

    public event EventHandler PropertyChanged;

    public virtual void Reset(string propertyName)
    {
      if (!this._connectionStringBuilder.ContainsKey(propertyName))
        return;
      this._connectionStringBuilder.Remove(propertyName);
      this.OnPropertyChanged(EventArgs.Empty);
    }

    public virtual bool IsComplete => true;

    public virtual void Test()
    {
      string testString = this.ToTestString();
      if (testString == null || testString.Length == 0)
        throw new InvalidOperationException(SR.AdoDotNetConnectionProperties_NoProperties);
      DbConnection connection = DbProviderFactories.GetFactory(this._providerName).CreateConnection();
      try
      {
        connection.ConnectionString = testString;
        connection.Open();
        this.Inspect(connection);
      }
      finally
      {
        connection.Dispose();
      }
    }

    public override string ToString() => this.ToFullString();

    public virtual string ToFullString() => this._connectionStringBuilder.ConnectionString;

    public virtual string ToDisplayString()
    {
      PropertyDescriptorCollection properties = this.GetProperties(new Attribute[1]
      {
        (Attribute) PasswordPropertyTextAttribute.Yes
      });
      List<KeyValuePair<string, object>> keyValuePairList = new List<KeyValuePair<string, object>>();
      foreach (MemberDescriptor memberDescriptor in properties)
      {
        string displayName = memberDescriptor.DisplayName;
        if (this.ConnectionStringBuilder.ShouldSerialize(displayName))
        {
          keyValuePairList.Add(new KeyValuePair<string, object>(displayName, this.ConnectionStringBuilder[displayName]));
          this.ConnectionStringBuilder.Remove(displayName);
        }
      }
      try
      {
        return this.ConnectionStringBuilder.ConnectionString;
      }
      finally
      {
        foreach (KeyValuePair<string, object> keyValuePair in keyValuePairList)
        {
          if (keyValuePair.Value != null)
            this.ConnectionStringBuilder[keyValuePair.Key] = keyValuePair.Value;
        }
      }
    }

    public DbConnectionStringBuilder ConnectionStringBuilder => this._connectionStringBuilder;

    protected virtual PropertyDescriptor DefaultProperty
    {
      get => TypeDescriptor.GetDefaultProperty((object) this._connectionStringBuilder, true);
    }

    protected virtual PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      return TypeDescriptor.GetProperties((object) this._connectionStringBuilder, attributes);
    }

    protected virtual void OnPropertyChanged(EventArgs e)
    {
      if (this.PropertyChanged == null)
        return;
      this.PropertyChanged((object) this, e);
    }

    protected virtual string ToTestString() => this._connectionStringBuilder.ConnectionString;

    protected virtual void Inspect(DbConnection connection)
    {
    }

    string ICustomTypeDescriptor.GetClassName()
    {
      return TypeDescriptor.GetClassName((object) this._connectionStringBuilder, true);
    }

    string ICustomTypeDescriptor.GetComponentName()
    {
      return TypeDescriptor.GetComponentName((object) this._connectionStringBuilder, true);
    }

    AttributeCollection ICustomTypeDescriptor.GetAttributes()
    {
      return TypeDescriptor.GetAttributes((object) this._connectionStringBuilder, true);
    }

    object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
    {
      return TypeDescriptor.GetEditor((object) this._connectionStringBuilder, editorBaseType, true);
    }

    TypeConverter ICustomTypeDescriptor.GetConverter()
    {
      return TypeDescriptor.GetConverter((object) this._connectionStringBuilder, true);
    }

    PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() => this.DefaultProperty;

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
    {
      return this.GetProperties(new Attribute[0]);
    }

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
    {
      return this.GetProperties(attributes);
    }

    EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
    {
      return TypeDescriptor.GetDefaultEvent((object) this._connectionStringBuilder, true);
    }

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
    {
      return TypeDescriptor.GetEvents((object) this._connectionStringBuilder, true);
    }

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
    {
      return TypeDescriptor.GetEvents((object) this._connectionStringBuilder, attributes, true);
    }

    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd)
    {
      return (object) this._connectionStringBuilder;
    }
  }
}
