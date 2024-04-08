using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.ConnectionUI
{
  /// <summary>Provides a set of methods and properties that enable the Data Connection dialog box to interact with a specified data provider's connection properties.</summary>
  public interface IDataConnectionProperties
  {
    /// <summary>Resets all connection properties and restores the object to its initial state.</summary>
    void Reset();

    /// <summary>Parses a data connection string that is built from a set of properties into the corresponding set of connection properties.</summary>
    /// <param name="s">The connection string that is being parsed.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="s" /> parameter is null.</exception>
    /// <exception cref="T:System.FormatException">The format specified for <paramref name="s" /> in not valid.</exception>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "0#s")]
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "s")]
    void Parse(string s);

    /// <summary>Retrieves a Boolean value indicating whether the specified set of connection properties is extensible; that is, whether it is possible to add and remove custom properties to the set of connection properties.</summary>
    /// <returns>true if the connection properties are extensible; otherwise, false.</returns>
    bool IsExtensible { get; }

    /// <summary>Adds a custom property to the existing set of data connection properties recognized by the data provider.</summary>
    /// <param name="propertyName">Name of the custom property added to the existing set of connection properties.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="propertyName" /> parameter is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">The specified connection properties are not extensible.</exception>
    void Add(string propertyName);

    /// <summary>Tests whether a given set of connection properties contains a specified property.</summary>
    /// <returns>true if the set of connection properties contains the specified property; otherwise, false.</returns>
    /// <param name="propertyName">Name of the property whose presence is being tested.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="propertyName" /> parameter is null.</exception>
    bool Contains(string propertyName);

    /// <summary>Represents a property instance of specified type and value.</summary>
    /// <returns>A property object instance of the specified name.</returns>
    /// <param name="propertyName">Name of the property.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="propertyName" /> parameter is null.</exception>
    /// <exception cref="T:System.InvalidCastException">When setting a property value, the specified value cannot be converted to the property type.</exception>
    object this[string propertyName] { get; set; }

    /// <summary>Removes a custom property from a specified set of data connection properties.</summary>
    /// <param name="propertyName">Name of the custom property to be removed.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="propertyName" /> parameter is null.</exception>
    void Remove(string propertyName);

    /// <summary>Event that is raised when a data provider connection property is changed.</summary>
    event EventHandler PropertyChanged;

    /// <summary>Resets a specified connection property to its initial value.</summary>
    /// <param name="propertyName">Name of the connection property being set to its default value.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="propertyName" /> parameter is null.</exception>
    void Reset(string propertyName);

    /// <summary>Retrieves a Boolean value indicating whether the current set of connection property values provides sufficient information to open a connection to the data source.</summary>
    /// <returns>true if the connection properties are complete; otherwise, false.</returns>
    bool IsComplete { get; }

    /// <summary>Tests whether the current set of connection properties can successfully open a connection.</summary>
    void Test();

    /// <summary>Retrieves the complete connection string representing the current set of connection properties.</summary>
    /// <returns>The entire connection string, including secure or sensitive information.</returns>
    string ToFullString();

    /// <summary>Retrieves a connection string for on-screen display reflecting the current set of connection properties, minus "sensitive" information that should not be displayed.</summary>
    /// <returns>The set of connection properties that are suitable for display on-screen.</returns>
    string ToDisplayString();
  }
}
