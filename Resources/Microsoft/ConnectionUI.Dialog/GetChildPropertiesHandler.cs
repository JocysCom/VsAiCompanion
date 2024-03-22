using System;
using System.ComponentModel;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  internal delegate PropertyDescriptorCollection GetChildPropertiesHandler(
    object instance,
    Attribute[] filter);
}
