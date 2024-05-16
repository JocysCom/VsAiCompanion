using System;
using System.Collections.Generic;

namespace JocysCom.ClassLibrary
{
	/// <summary>
	/// Encapsulates the outcome of an operation including result, status code, and errors.
	/// </summary>
	/// <typeparam name="T">Type of result value.</typeparam>
	public class OperationResult<T>
	{
		/// <summary>
		/// Default constructor assigns a unique identifier and initializes the error list.
		/// </summary>
		public OperationResult()
		{
			RequestId = Guid.NewGuid().ToString("N");
		}

		/// <summary>
		/// Constructor for success scenarios, setting result and OK status.
		/// </summary>
		/// <param name="result">Result value of the operation.</param>
		public OperationResult(T result) : this()
		{
			StatusCode = 0;
			StatusText = "Success";
			Result = result;
		}

		/// <summary>
		/// Constructor for failure scenarios, setting errors and Internal Server Error status.
		/// </summary>
		/// <param name="error">Exception related to operation failure.</param>
		public OperationResult(Exception error) : this()
		{
			StatusCode = 1;
			StatusText = error.Message;
			Errors = new List<string>();
			Errors.Add(error.ToString());
		}


		/// <summary>
		/// Constructor for failure scenarios, setting errors and Internal Server Error status.
		/// </summary>
		/// <param name="errors">Collection of exceptions related to operation failure.</param>
		public OperationResult(IEnumerable<Exception> errors) : this()
		{
			StatusCode = 1;
			StatusText = "Error";
			Errors = new List<string>();
			foreach (var error in errors)
				Errors.Add(error.ToString());
		}

		/// <summary>
		/// Constructor for handling both successful and failed operations with customizable parameters.
		/// </summary>
		/// <param name="result">Result value of the operation.</param>
		/// <param name="statusCode">Status code representing operation outcome.</param>
		/// <param name="statusText">Descriptive text providing additional details.</param>
		/// <param name="errors">Collection of exceptions related to operation failure.</param>
		public OperationResult(T result, int statusCode, string statusText, IEnumerable<Exception> errors) : this()
		{
			Result = result;
			StatusCode = statusCode;
			StatusText = statusText ?? statusCode.ToString();
			Errors = new List<string>();
			foreach (var error in errors)
				Errors.Add(error.ToString());
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
		/// Result value of the operation.
		/// </summary>
		public T Result { get; set; }

	}
}
