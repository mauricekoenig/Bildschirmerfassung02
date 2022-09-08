
using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.ServiceProcess;
using System.Net.Http.Headers;
using System.Text;
using System.IO;

namespace Bildschirmerfassung
{
    public partial class BildschirmService : ServiceBase
    {

        private HttpClient _client;
        private bool _trustDangerous;
        private HttpClientHandler _handler;
        private string _endPoint;

        private Dictionary<string, string> _userInfo;
        public string _configFilePath;

        private string _loginName;
        private string _loginPassword;


        protected override void OnStart(string[] args) 
            => InitialSetup();

        private void InitialSetup ()
        {
            _trustDangerous = true;
            _userInfo = GetUserInfo();
            SystemEvents.SessionSwitch += OnSessionSwitch;

            _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "abe.config");
            GetParsedConfig();

            if (_trustDangerous)
            {
                _handler = new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                _client = new HttpClient(_handler);
            }

            else 
            _client = new HttpClient();
        }

        private void GetParsedConfig ()
        {

            string config = File.ReadAllText(_configFilePath);
            string[] configLines = config.Split('\n');
            // Reihenfolge: Endpunkt-Link [0], Benutzername [1], Password [2]

            _endPoint = configLines[0];
            _loginName = configLines[1]; 
            _loginPassword = configLines[2];
        }

        protected async void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
        {

            FormUrlEncodedContent form;

            if (args.Reason == SessionSwitchReason.SessionLock)
            {

                form = CreateForm(EventType.LockSession);
                var requestMessage = CreateRequestMessage(form, _endPoint);
                requestMessage.Headers.Authorization = CreateAuthHeader(_loginName, _loginPassword);
                await _client.SendAsync(requestMessage);
                return;
            }

            if (args.Reason == SessionSwitchReason.SessionUnlock)
            {

                form = CreateForm(EventType.UnlockSession);
                var requestMessage = CreateRequestMessage(form, _endPoint);
                requestMessage.Headers.Authorization = CreateAuthHeader(_loginName, _loginPassword);
                await _client.SendAsync(requestMessage);
                return;
            }
        }

        private AuthenticationHeaderValue CreateAuthHeader(string name, string pw)
        {
            var bytes = Encoding.UTF8.GetBytes($"{name}:{pw}");
            var base64 = Convert.ToBase64String(bytes);
            return new AuthenticationHeaderValue("Basic", base64);
        }

        private HttpRequestMessage CreateRequestMessage(HttpContent content, string address)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, address);
            request.Content = content;
            return request;
        }

        private FormUrlEncodedContent CreateForm(EventType type)
        {
            switch (type)
            {

                case EventType.LockSession:
                    return new FormUrlEncodedContent(new Dictionary<string, string>() {
                        {"Locked", "Placeholder" },
                        {"Name", _userInfo["Name"]},
                        {"Vorname", _userInfo["Vorname"]},
                        {"DateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}
                    });

                case EventType.UnlockSession:
                    return new FormUrlEncodedContent(new Dictionary<string, string>() {
                        {"Unlocked", "Placeholder" },
                        {"DateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                        {"Name", _userInfo["Name"]},
                        {"Vorname", _userInfo["Vorname"]}
                     }); ;

                default:
                    return null;
            }
        }

        private enum EventType
        {
            LockSession,
            UnlockSession
        }

        private Dictionary<string, string> GetUserInfo()
        {

            var userName = Environment.UserName;
            var words = userName.Split('.');

            char.ToUpper(words[0][0]);
            char.ToUpper(words[1][0]);

            return new Dictionary<string, string>() { { "Vorname", words[0] }, { "Name", words[1] } };
        }

    }
}
