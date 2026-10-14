using System.Globalization;

namespace Arlink28.Api.Helpers;

public class AppException : Exception
{
    public AppException() : base() { }

    public AppException(string message) : base(message) { }

    public AppException(string message, params object[] args)
        : base(string.Format(CultureInfo.CurrentCulture, message, args)) { }
}

/// <summary>The request is valid but clashes with the current state (a taken slug, a destination still in use). Returned as 409.</summary>
public class ConflictException(string message) : AppException(message);
