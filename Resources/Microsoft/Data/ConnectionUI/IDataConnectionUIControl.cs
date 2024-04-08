namespace Microsoft.Data.ConnectionUI
{
  /// <summary>Provides a set of methods and properties through which the Data Connection dialog box interacts with a third-party data connection user interface (UI) control, which is shown as the body of the Data Connection dialog box.</summary>
  public interface IDataConnectionUIControl
  {
    /// <summary>Initializes the data connection user interface (UI) control with an instance of the <see cref="T:Microsoft.Data.ConnectionUI.IDataConnectionProperties" /> interface, which serves as the store for the data shown on the data connection UI control.</summary>
    /// <param name="connectionProperties">The set of connection properties serving as stores for data shown on the data connection UI control.</param>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="connectionProperties" /> parameter is null.</exception>
    /// <exception cref="T:System.ArgumentException">The <paramref name="connectionProperties" /> parameter is not a valid implementation of DataConnectionProperties understood by this connection UI control.</exception>
    void Initialize(IDataConnectionProperties connectionProperties);

    /// <summary>Loads connection property values into the data connection UI controls from an instance of the <see cref="T:Microsoft.Data.ConnectionUI.IDataConnectionProperties" /> interface.</summary>
    void LoadProperties();
  }
}
