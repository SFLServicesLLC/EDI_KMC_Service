using log4net;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace CSCFileService
{
    class ClsFunctions
    {
		private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		/// <summary>
		/// This is a function to send out an email
		/// </summary>
		/// <param name="ToEmail"></param>
		/// <param name="Subject"></param>
		/// <param name="FileName"></param>
		/// <param name="FromEmail"></param>
		/// <param name="SMTPServer"></param>
		/// <param name="SMTPPort"></param>
		/// <param name="SMTPUser"></param>
		/// <param name="SMTPPassword"></param>
		/// <exception cref="Exception"></exception>
		public static void NetEmail(string ToEmail, string Subject, string FileName, string FromEmail, string SMTPServer, int SMTPPort, string SMTPUser, string SMTPPassword)
		{
			try
			{
				// Create Message
				MailMessage oMail = new MailMessage();
				oMail.IsBodyHtml = true;

				// set the addresses
				oMail.From = new MailAddress(FromEmail);
				oMail.To.Add(ToEmail);

				// set the content
				oMail.Subject = Subject;
				oMail.Body = "This email is from our MyFFLbook system.";

				// add an attachment from the filesystem
				oMail.Attachments.Add(new Attachment("" + FileName + ""));
				oMail.Priority = MailPriority.High;

				SmtpClient client = new SmtpClient(SMTPServer, SMTPPort);
				client.EnableSsl = true;

				if (!(SMTPUser == null) && !(SMTPPassword == null))
				{
					client.UseDefaultCredentials = false;
					System.Net.NetworkCredential x = new System.Net.NetworkCredential(SMTPUser, SMTPPassword);
					client.Credentials = x;
					client.DeliveryMethod = SmtpDeliveryMethod.Network;
				}

				client.SendAsync(oMail, null/* TODO Change to default(_) if this is not a reference type */);
			}

			// Dim smtp As New System.Net.Mail.SmtpClient(SMTPServer, SMTPPort)
			// smtp.Send(oMail)

			catch (Exception ex)
			{
				_log.Error(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
				throw new Exception(ex.ToString() + Constants.vbCrLf + ex.StackTrace.ToString());
			}
		}

	}
}
