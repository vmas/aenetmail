using System;
using System.Text.RegularExpressions;

namespace AE.Net.Mail {
	public class Pop3Client : TextClient, IMailClient {
		public Pop3Client() { }
		public Pop3Client(string host, string username, string password, int port = 110, bool secure = false, bool skipSslValidation = false) {
			Connect(host, port, secure, skipSslValidation);
			Login(username, password);
		}

		internal override void OnLogin(string username, string password) {
			SendCommandCheckOK("USER " + username);
			SendCommandCheckOK("PASS " + password);
		}

		internal override void OnLogout() {
			if (this.Stream != null) {
				SendCommand("QUIT");
			}
		}

		internal override void CheckResultOK(string result) {
			if (!result.StartsWith("+OK", StringComparison.OrdinalIgnoreCase)) {
				throw new UnexpectedResponseException(result.Substring(result.IndexOf(' ') + 1).Trim());
			}
		}

		public virtual int GetMessageCount() {
			CheckAuthenticationStatus();
			var result = SendCommandGetResponse("STAT");
			CheckResultOK(result);
			string[] data = result.Split(' ');
			int count;
			if (data.Length > 1 && int.TryParse(data[1], out count))
				return count;
			return 0;
		}

		public virtual MailMessage GetMessage(int index, bool headersOnly = false) {
			return GetMessage((index + 1).ToString(), headersOnly);
		}

		private static Regex rxOctets = new Regex(@"(\d+)\s+octets", RegexOptions.IgnoreCase);
		public virtual MailMessage GetMessage(string uid, bool headersOnly = false) {
			CheckAuthenticationStatus();
			var line = SendCommandGetResponse(string.Format(headersOnly ? "TOP {0} 0" : "RETR {0}", uid));
			var size = rxOctets.Match(line).Groups[1].Value.ToInt();
			CheckResultOK(line);
			var msg = new MailMessage();
			msg.Load(this.Stream, headersOnly, size, '.');

			msg.Uid = uid;
			var last = GetResponse();
			if (string.IsNullOrEmpty(last))
				last = GetResponse();

			if (last != ".") {
				if (System.Diagnostics.Debugger.IsAttached)
					System.Diagnostics.Debugger.Break();
				else
					throw new UnexpectedResponseException(last);
				RaiseWarning(msg, "Expected \".\" in stream, but received \"" + last + "\"");
			}

			return msg;
		}

		public virtual void DeleteMessage(string uid) {
			SendCommandCheckOK("DELE " + uid);

		}

		public virtual void DeleteMessage(int index) {
			DeleteMessage((index + 1).ToString());
		}

		public virtual void DeleteMessage(AE.Net.Mail.MailMessage msg) {
			DeleteMessage(msg.Uid);
		}
	}
}