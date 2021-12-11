using PaygatePaySubs.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace PaygatePaySubs.Controllers
{
    public class HomeController : Controller
    {
        private IPayment _payment = new Services.Payment();

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> Recurring()
        {
            HttpClient http = new HttpClient();
            Dictionary<string, string> request = new Dictionary<string, string>();
            string paymentAmount = (50 * 100).ToString("00"); // amount int cents e.i 50 rands is 5000 cents

            request.Add("VERSION", 21.ToString());
            request.Add("PAYGATE_ID", "10011072130");
            request.Add("REFERENCE", "Customer1"); // Payment ref e.g ORDER NUMBER
            request.Add("AMOUNT", paymentAmount);
            request.Add("CURRENCY", "ZAR"); // South Africa
            //request.Add("RETURN_URL", $"{Request.Url.Scheme}://{Request.Url.Authority}/PayGate/completepayment");
            request.Add("RETURN_URL", "https://www.google.co.za/");
            request.Add("TRANSACTION_DATE", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            request.Add("SUBS_START_DATE", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            request.Add("SUBS_END_DATE", DateTime.Now.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss"));
            request.Add("SUBS_FREQUENCY", 228.ToString());
            request.Add("PROCESS_NOW", "NO");
            request.Add("PROCESS_NOW_AMOUNT", "");
            //request.Add("EMAIL", "mukki7867@gmail.com");
            request.Add("CHECKSUM", _payment.GetMd5Hash(request, "secret"));

            string requestString = _payment.ToUrlEncodedString(request);
            StringContent content = new StringContent(requestString, Encoding.UTF8, "application/x-www-form-urlencoded");
            HttpResponseMessage response = await http.PostAsync("https://www.paygate.co.za/paysubs/process.trans", content);
            // if the request did not succeed, this line will make the program crash
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync();

            Dictionary<string, string> results = _payment.ToDictionary(responseContent);

            //CRASHES HERE

            if (results.Keys.Contains("ERROR"))
            {
                return Json(new
                {
                    success = false,
                    message = "An error occured while initiating your request"
                }, JsonRequestBehavior.AllowGet);
            }

            if (!_payment.VerifyMd5Hash(results, "secret", results["CHECKSUM"]))
            {
                return Json(new
                {
                    success = false,
                    message = "MD5 verification failed"
                }, JsonRequestBehavior.AllowGet);
            }
            var IsRecorded = true;  
            if (IsRecorded)
            {
                return Json(new
                {
                    success = true,
                    message = "Request completed successfully",
                    results
                }, JsonRequestBehavior.AllowGet);
            }
            return Json(new
            {
                success = false,
                message = "Failed to record a transaction"
            }, JsonRequestBehavior.AllowGet);
        }

        // This is your return url from Paygate
        // This will run when you complete payment
        [HttpPost]
        public ActionResult CompletePayment()
        {
            string responseContent = Request.Params.ToString();
            Dictionary<string, string> results = _payment.ToDictionary(responseContent);

            // Reorder attributes for MD5 check
            Dictionary<string, string> validationSet = new Dictionary<string, string>();
            validationSet.Add("PAYGATE_ID", "10011072130");
            validationSet.Add("REFERENCE", results["REFERENCE"]);
            validationSet.Add("RESULT_CODE", results["RESULT_CODE"]);
            validationSet.Add("RESULT_DESC", results["RESULT_DESC"]);
            validationSet.Add("SUBSCRIPTION_ID", results["SUBSCRIPTION_ID"]);
            validationSet.Add("PAY_REQUEST_ID", results["PAY_REQUEST_ID"]);
            validationSet.Add("TRANSACTION_STATUS", results["TRANSACTION_STATUS"]);
            //validationSet.Add("REFERENCE", transaction.REFERENCE);

            if (!_payment.VerifyMd5Hash(validationSet, "secret", results["CHECKSUM"]))
            {
                // checksum error
                return RedirectToAction("Failed");
            }
            /** Payment Status 
             * -2 = Unable to reconsile transaction
             * -1 = Checksum Error
             * 0 = Pending
             * 1 = Approved
             * 2 = Declined
             * 3 = Cancelled
             * 4 = User Cancelled
             */
            int paymentStatus = int.Parse(results["TRANSACTION_STATUS"]);
            if (paymentStatus == 1)
            {
                // Yey, payment approved
                // Do something useful
            }
            // Query paygate transaction details
            // And update user transaction on your database
            //await VerifyTransaction(responseContent, transaction.REFERENCE);
            return RedirectToAction("Complete", new { id = results["TRANSACTION_STATUS"] });
        }

        private async Task VerifyTransaction(string responseContent, string Referrence)
        {
            HttpClient client = new HttpClient();
            Dictionary<string, string> response = _payment.ToDictionary(responseContent);
            Dictionary<string, string> request = new Dictionary<string, string>();

            request.Add("PAYGATE_ID", "10011072130");
            request.Add("PAY_REQUEST_ID", response["PAY_REQUEST_ID"]);
            request.Add("REFERENCE", Referrence);
            request.Add("CHECKSUM", _payment.GetMd5Hash(request, "secret"));

            string requestString = _payment.ToUrlEncodedString(request);

            StringContent content = new StringContent(requestString, Encoding.UTF8, "application/x-www-form-urlencoded");

            // ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            HttpResponseMessage res = await client.PostAsync("https://secure.paygate.co.za/payweb3/query.trans", content);
            res.EnsureSuccessStatusCode();

            string _responseContent = await res.Content.ReadAsStringAsync();

            Dictionary<string, string> results = _payment.ToDictionary(_responseContent);
            if (!results.Keys.Contains("ERROR"))
            {
                //_payment.UpdateTransaction(results, results["PAY_REQUEST_ID"]);
            }

        }

        public ViewResult Complete(int? id)
        {
            string status = "Unknown";
            switch (id.ToString())
            {
                case "-2":
                    status = "Unable to reconsile transaction";
                    break;
                case "-1":
                    status = "Checksum Error. The values have been altered";
                    break;
                case "0":
                    status = "Not Done";
                    break;
                case "1":
                    status = "Approved";
                    break;
                case "2":
                    status = "Declined";
                    break;
                case "3":
                    status = "Cancelled";
                    break;
                case "4":
                    status = "User Cancelled";
                    break;
                default:
                    status = $"Unknown Status({ id })";
                    break;
            }
            TempData["Status"] = status;

            return View();
        }

    }
}