using System;
using System.Collections.Generic;
using System.Linq;

namespace JocysCom.ClassLibrary
{
	/// <summary>
	/// Encapsulates the outcome of an operation, including a unique RequestId, result value, status code, and errors.
	/// </summary>
	/// <typeparam name="T">Type of result value.</typeparam>
	public class OperationResult<T>
	{
		/// <summary>Initializes a new instance with a unique RequestId.</summary>
		public OperationResult()
		{
			RequestId = Guid.NewGuid().ToString("N");
		}

		/// <summary>Initializes a successful result: sets Data, StatusCode = 0, and StatusText to "Success".</summary>
		/// <param name="data">Result data value of the operation.</param>
		public OperationResult(T data) : this()
		{
			StatusCode = 0;
			StatusText = "Success";
			Data = data;
		}

		/// <summary>Initializes a failure result: sets StatusCode = 1, StatusText to the exception message, and populates Errors with exception details.</summary>
		/// <param name="error">Exception related to operation failure.</param>
		public OperationResult(Exception error) : this()
		{
			StatusCode = 1;
			StatusText = error.Message;
			Errors = new List<string>();
			Errors.Add(error.ToString());
		}

		/// <summary>Initializes a failure result: sets StatusCode = 1, StatusText to "Error", and populates Errors with details of each exception.</summary>
		/// <param name="errors">Collection of exceptions related to operation failure.</param>
		public OperationResult(IEnumerable<Exception> errors) : this()
		{
			StatusCode = 1;
			StatusText = "Error";
			Errors = new List<string>();
			foreach (var error in errors)
				Errors.Add(error.ToString());
		}

		/// <summary>Initializes a result with provided Data, StatusCode, StatusText (fallback to code if null), and optional Errors.</summary>
		/// <param name="data">Result data value of the operation.</param>
		/// <param name="statusCode">Status code representing operation outcome.</param>
		/// <param name="statusText">Descriptive text providing additional details (fallback to statusCode.ToString() if null).</param>
		/// <param name="errors">Collection of exceptions related to operation failure.</param>
		public OperationResult(T data, int statusCode, string statusText, IEnumerable<Exception> errors = null) : this()
		{
			Data = data;
			StatusCode = statusCode;
			StatusText = statusText ?? statusCode.ToString();
			Errors = new List<string>();
			if (errors != null)
				foreach (var error in errors)
					Errors.Add(error.ToString());
		}

		/// <summary>Maps this instance to OperationResult&lt;T2&gt;, copying Data, StatusCode, StatusText, and Errors.</summary>
		/// <typeparam name="T2">Type of result value for the new OperationResult.</typeparam>
		/// <param name="data">Result data value for the new OperationResult.</param>
		public OperationResult<T2> ToResult<T2>(T2 data)
		{
			var newResult = new OperationResult<T2>();
			newResult.Data = data;
			newResult.StatusCode = StatusCode;
			newResult.StatusText = StatusText;
			newResult.Errors = Errors?.ToList();
			return newResult;
		}

		/// <summary>
		/// Unique identifier for the operation used for tracking and logging.
		/// </summary>
		public string RequestId { get; }

		/// <summary>
		/// Indicates whether the operation was successful.
		/// </summary>
		public bool Success => StatusCode == 0;

		/// <summary>
		/// Numeric status code representing operation outcome.
		/// </summary>
		public int StatusCode { get; set; }

		/// <summary>
		/// Descriptive text providing additional operation outcome details.
		/// </summary>
		public string StatusText { get; set; }

		/// <summary>
		/// List of exceptions related to operation failure.
		/// </summary>
		public List<string> Errors { get; private set; }

		/// <summary>
		/// Result data value of the operation.
		/// </summary>
		public T Data { get; set; }
	}
}