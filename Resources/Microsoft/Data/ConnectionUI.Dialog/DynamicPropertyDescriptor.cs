using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Microsoft.Data.ConnectionUI
{
  internal class DynamicPropertyDescriptor : PropertyDescriptor
  {
    private string _name;
    private string _category;
    private string _description;
    private Type _propertyType;
    private string _converterTypeName;
    private TypeConverter _converter;
    private List<Attribute> _attributes;
    private GetValueHandler _getValueHandler;
    private SetValueHandler _setValueHandler;
    private CanResetValueHandler _canResetValueHandler;
    private ResetValueHandler _resetValueHandler;
    private ShouldSerializeValueHandler _shouldSerializeValueHandler;
    private GetChildPropertiesHandler _getChildPropertiesHandler;
    private Type _componentType;
    private PropertyDescriptor _baseDescriptor;

    public DynamicPropertyDescriptor(string name)
      : base(name, (Attribute[]) null)
    {
    }

    public DynamicPropertyDescriptor(string name, params Attribute[] attributes)
      : base(name, DynamicPropertyDescriptor.FilterAttributes(attributes))
    {
    }

    public DynamicPropertyDescriptor(PropertyDescriptor baseDescriptor)
      : this(baseDescriptor, (Attribute[]) null)
    {
    }

    public DynamicPropertyDescriptor(
      PropertyDescriptor baseDescriptor,
      params Attribute[] newAttributes)
      : base((MemberDescriptor) baseDescriptor, newAttributes)
    {
      this.AttributeArray = DynamicPropertyDescriptor.FilterAttributes(this.AttributeArray);
      this._baseDescriptor = baseDescriptor;
    }

    public override string Name => this._name != null ? this._name : base.Name;

    public override string Category => this._category != null ? this._category : base.Category;

    public override string Description
    {
      get => this._description != null ? this._description : base.Description;
    }

    public override Type PropertyType
    {
      get
      {
        if (this._propertyType != (Type) null)
          return this._propertyType;
        return this._baseDescriptor != null ? this._baseDescriptor.PropertyType : (Type) null;
      }
    }

    public override bool IsReadOnly
    {
      get => ReadOnlyAttribute.Yes.Equals((object) this.Attributes[typeof (ReadOnlyAttribute)]);
    }

    public override TypeConverter Converter
    {
      get
      {
        if (this._converterTypeName != null)
        {
          if (this._converter == null)
          {
            Type typeFromName = this.GetTypeFromName(this._converterTypeName);
            if (typeof (TypeConverter).IsAssignableFrom(typeFromName))
              this._converter = (TypeConverter) this.CreateInstance(typeFromName);
          }
          if (this._converter != null)
            return this._converter;
        }
        return base.Converter;
      }
    }

    public override AttributeCollection Attributes
    {
      get
      {
        if (this._attributes != null)
        {
          Dictionary<object, Attribute> dictionary = new Dictionary<object, Attribute>();
          foreach (Attribute attribute in this.AttributeArray)
            dictionary[attribute.TypeId] = attribute;
          foreach (Attribute attribute in this._attributes)
          {
            if (!attribute.IsDefaultAttribute())
              dictionary[attribute.TypeId] = attribute;
            else if (dictionary.ContainsKey(attribute.TypeId))
              dictionary.Remove(attribute.TypeId);
            if (attribute is CategoryAttribute categoryAttribute)
              this._category = categoryAttribute.Category;
            if (attribute is DescriptionAttribute descriptionAttribute)
              this._description = descriptionAttribute.Description;
            if (attribute is TypeConverterAttribute converterAttribute)
            {
              this._converterTypeName = converterAttribute.ConverterTypeName;
              this._converter = (TypeConverter) null;
            }
          }
          Attribute[] array = new Attribute[dictionary.Values.Count];
          dictionary.Values.CopyTo(array, 0);
          this.AttributeArray = array;
          this._attributes = (List<Attribute>) null;
        }
        return base.Attributes;
      }
    }

    public GetValueHandler GetValueHandler
    {
      get => this._getValueHandler;
      set => this._getValueHandler = value;
    }

    public SetValueHandler SetValueHandler
    {
      get => this._setValueHandler;
      set => this._setValueHandler = value;
    }

    public CanResetValueHandler CanResetValueHandler
    {
      get => this._canResetValueHandler;
      set => this._canResetValueHandler = value;
    }

    public ResetValueHandler ResetValueHandler
    {
      get => this._resetValueHandler;
      set => this._resetValueHandler = value;
    }

    public ShouldSerializeValueHandler ShouldSerializeValueHandler
    {
      get => this._shouldSerializeValueHandler;
      set => this._shouldSerializeValueHandler = value;
    }

    public GetChildPropertiesHandler GetChildPropertiesHandler
    {
      get => this._getChildPropertiesHandler;
      set => this._getChildPropertiesHandler = value;
    }

    public override Type ComponentType
    {
      get
      {
        if (this._componentType != (Type) null)
          return this._componentType;
        return this._baseDescriptor != null ? this._baseDescriptor.ComponentType : (Type) null;
      }
    }

    public void SetName(string value)
    {
      if (value == null)
        value = string.Empty;
      this._name = value;
    }

    public void SetDisplayName(string value)
    {
      if (value == null)
        value = DisplayNameAttribute.Default.DisplayName;
      this.SetAttribute((Attribute) new DisplayNameAttribute(value));
    }

    public void SetCategory(string value)
    {
      if (value == null)
        value = CategoryAttribute.Default.Category;
      this._category = value;
      this.SetAttribute((Attribute) new CategoryAttribute(value));
    }

    public void SetDescription(string value)
    {
      if (value == null)
        value = DescriptionAttribute.Default.Description;
      this._description = value;
      this.SetAttribute((Attribute) new DescriptionAttribute(value));
    }

    public void SetPropertyType(Type value)
    {
      this._propertyType = !(value == (Type) null) ? value : throw new ArgumentNullException(nameof (value));
    }

    public void SetDesignTimeOnly(bool value)
    {
      this.SetAttribute((Attribute) new DesignOnlyAttribute(value));
    }

    public void SetIsBrowsable(bool value)
    {
      this.SetAttribute((Attribute) new BrowsableAttribute(value));
    }

    public void SetIsLocalizable(bool value)
    {
      this.SetAttribute((Attribute) new LocalizableAttribute(value));
    }

    public void SetIsReadOnly(bool value)
    {
      this.SetAttribute((Attribute) new ReadOnlyAttribute(value));
    }

    public void SetConverterType(Type value)
    {
      this._converterTypeName = value != (Type) null ? value.AssemblyQualifiedName : (string) null;
      if (this._converterTypeName != null)
        this.SetAttribute((Attribute) new TypeConverterAttribute(value));
      else
        this.SetAttribute((Attribute) TypeConverterAttribute.Default);
      this._converter = (TypeConverter) null;
    }

    public void SetAttribute(Attribute value)
    {
      if (value == null)
        throw new ArgumentNullException(nameof (value));
      if (this._attributes == null)
        this._attributes = new List<Attribute>();
      this._attributes.Add(value);
    }

    public void SetAttributes(params Attribute[] values)
    {
      foreach (Attribute attribute in values)
        this.SetAttribute(attribute);
    }

    public void SetComponentType(Type value) => this._componentType = value;

    public override object GetValue(object component)
    {
      if (this.GetValueHandler != null)
        return this.GetValueHandler(component);
      return this._baseDescriptor != null ? this._baseDescriptor.GetValue(component) : (object) null;
    }

    public override void SetValue(object component, object value)
    {
      if (this.SetValueHandler != null)
      {
        this.SetValueHandler(component, value);
        this.OnValueChanged(component, EventArgs.Empty);
      }
      else
      {
        if (this._baseDescriptor == null)
          return;
        this._baseDescriptor.SetValue(component, value);
        this.OnValueChanged(component, EventArgs.Empty);
      }
    }

    public override bool CanResetValue(object component)
    {
      if (this.CanResetValueHandler != null)
        return this.CanResetValueHandler(component);
      return this._baseDescriptor != null ? this._baseDescriptor.CanResetValue(component) : this.Attributes[typeof (DefaultValueAttribute)] != null;
    }

    public override void ResetValue(object component)
    {
      if (this.ResetValueHandler != null)
        this.ResetValueHandler(component);
      else if (this._baseDescriptor != null)
      {
        this._baseDescriptor.ResetValue(component);
      }
      else
      {
        if (!(this.Attributes[typeof (DefaultValueAttribute)] is DefaultValueAttribute attribute))
          return;
        this.SetValue(component, attribute.Value);
      }
    }

    public override bool ShouldSerializeValue(object component)
    {
      if (this.ShouldSerializeValueHandler != null)
        return this.ShouldSerializeValueHandler(component);
      if (this._baseDescriptor != null)
        return this._baseDescriptor.ShouldSerializeValue(component);
      return this.Attributes[typeof (DefaultValueAttribute)] is DefaultValueAttribute attribute && !object.Equals(this.GetValue(component), attribute.Value);
    }

    public override PropertyDescriptorCollection GetChildProperties(
      object instance,
      Attribute[] filter)
    {
      if (this.GetChildPropertiesHandler != null)
        return this.GetChildPropertiesHandler(instance, filter);
      return this._baseDescriptor != null ? this._baseDescriptor.GetChildProperties(instance, filter) : base.GetChildProperties(instance, filter);
    }

    protected override int NameHashCode
    {
      get => this._name != null ? this._name.GetHashCode() : base.NameHashCode;
    }

    private static Attribute[] FilterAttributes(Attribute[] attributes)
    {
      Dictionary<object, Attribute> dictionary = new Dictionary<object, Attribute>();
      foreach (Attribute attribute in attributes)
      {
        if (!attribute.IsDefaultAttribute())
          dictionary.Add(attribute.TypeId, attribute);
      }
      Attribute[] array = new Attribute[dictionary.Values.Count];
      dictionary.Values.CopyTo(array, 0);
      return array;
    }
  }
}
