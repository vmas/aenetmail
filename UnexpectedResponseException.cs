using System;

namespace AE.Net.Mail
{
	public class UnexpectedResponseException : NotSupportedException
	{
		public UnexpectedResponseException(string response)
		{
			this.Response = response;
		}

		public string Response { get; protected set; }
	}
}
