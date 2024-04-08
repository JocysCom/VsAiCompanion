using System;
using System.ComponentModel;

namespace Microsoft.Data.ConnectionUI
{
  internal delegate PropertyDescriptorCollection GetChildPropertiesHandler(
    object instance,
    Attribute[] filter);
}
