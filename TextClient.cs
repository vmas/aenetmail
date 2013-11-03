using System;
using System.IO;
using System.Net.Sockets;

namespace AE.Net.Mail {
	public abstract class TextClient : IDisposable {
		protected TcpClient _Connection;
		protected Stream _Stream;
		private int _timeout;

		public virtual string Host { get; private set; }
		public virtual int Port { get; set; }
		public virtual bool Ssl { get; set; }
		public virtual bool IsConnected { get; private set; }
		public virtual bool IsAuthenticated { get; private set; }
		public virtual bool IsDisposed { get; private set; }
		public virtual System.Text.Encoding Encoding { get; set; }

		public virtual int Timeout
		{
			get { return _timeout; }
			set
			{
				_timeout = value;
				if (_Connection != null)
				{
					_Connection.ReceiveTimeout = value;
					_Connection.SendTimeout = value;
				}
			}
		}

		public event EventHandler<WarningEventArgs> Warning;

		protected virtual void RaiseWarning(MailMessage mailMessage, string message) {
			var warning = Warning;
			if (warning != null) {
				warning(this, new WarningEventArgs { MailMessage = mailMessage, Message = message });
			}
		}

		public TextClient() {
			_Connection = new TcpClient();
			Encoding = System.Text.Encoding.UTF8;
			this.Timeout = 10000;
		}

		internal abstract void OnLogin(string username, string password);
		internal abstract void OnLogout();
		internal abstract void CheckResultOK(string result);

		protected virtual void OnConnected(string result) {
			CheckResultOK(result);
		}

		public virtual void Login(string username, string password) {
			CheckConnectionStatus();
			IsAuthenticated = false;
			OnLogin(username, password);
			IsAuthenticated = true;
		}

		public virtual void Logout() {
			IsAuthenticated = false;
			OnLogout();
		}


		public virtual void Connect(string hostname, int port, bool ssl, bool skipSslValidation) {
			System.Net.Security.RemoteCertificateValidationCallback validateCertificate = null;
			if (skipSslValidation)
				validateCertificate = (sender, cert, chain, err) => true;
			Connect(hostname, port, ssl, validateCertificate);
		}

		public virtual void Connect(string hostname, int port, bool ssl, System.Net.Security.RemoteCertificateValidationCallback validateCertificate) {
			try {
				Host = hostname;
				Port = port;
				Ssl = ssl;

				_Connection.SendTimeout = this.Timeout;
				_Connection.ReceiveTimeout = this.Timeout;
				IAsyncResult ar = _Connection.BeginConnect(hostname, port, null, null);
				System.Threading.WaitHandle wh = ar.AsyncWaitHandle;
				try
				{
					if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(this.Timeout), true))
					{
						_Connection.Close();
						throw new TimeoutException(string.Format("Could not connect to {0} on port {1}.", hostname, port));
					}
					_Connection.EndConnect(ar);
				}
				finally
				{
					wh.Close();
				}
				_Stream = _Connection.GetStream();
				if (ssl) {
					System.Net.Security.SslStream sslStream;
					if (validateCertificate != null)
						sslStream = new System.Net.Security.SslStream(_Stream, false, validateCertificate);
					else
						sslStream = new System.Net.Security.SslStream(_Stream, false);
					_Stream = sslStream;
					sslStream.AuthenticateAsClient(hostname);
				}

				OnConnected(GetResponse());

				IsConnected = true;
				Host = hostname;
			} catch (Exception) {
				IsConnected = false;
				Utilities.TryDispose(ref _Stream);
				throw;
			}
		}

		protected virtual void CheckConnectionStatus() {
			if (IsDisposed)
				throw new ObjectDisposedException(this.GetType().Name);
			if (!IsConnected)
				throw new Exception("You must connect first!");
		}

		protected virtual void CheckAuthenticationStatus()
		{
			CheckConnectionStatus();
			if (!IsAuthenticated)
				throw new Exception("You must authenticate first!");
		}

		protected virtual void SendCommand(string command) {
			var bytes = System.Text.Encoding.Default.GetBytes(command + "\r\n");
			_Stream.Write(bytes, 0, bytes.Length);
		}

		protected virtual string SendCommandGetResponse(string command) {
			SendCommand(command);
			return GetResponse();
		}

		protected virtual string GetResponse() {
			int max = 0;
			return _Stream.ReadLine(ref max, Encoding, null);
		}

		protected virtual void SendCommandCheckOK(string command) {
			CheckResultOK(SendCommandGetResponse(command));
		}

		public virtual void Disconnect() {
			if (IsAuthenticated)
				Logout();

			Utilities.TryDispose(ref _Stream);
			Utilities.TryDispose(ref _Connection);
		}

		~TextClient() {
			Dispose(false);
		}
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing) {
			if (!IsDisposed && disposing)
				lock (this)
					if (!IsDisposed && disposing) {
						IsDisposed = true;
						Disconnect();
						if (_Stream != null) _Stream.Dispose();
						if (_Connection != null) _Connection.Close();
					}

			_Stream = null;
			_Connection = null;
		}
	}
}
