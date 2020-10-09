using DPXTool.DPX.Model.Common;
using DPXTool.DPX.Model.JobInstances;
using DPXTool.DPX.Model.License;
using DPXTool.DPX.Model.Login;
using DPXTool.Util;
using Newtonsoft.Json;
using Refit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DPXTool.DPX
{
    /// <summary>
    /// Client to communicate with a DPX Master server above version 4.6.0 (using new REST api)
    /// </summary>
    public class DPXClient
    {
        /// <summary>
        /// Dpx api instance using ReFit
        /// </summary>
        DPXApi dpx;

        /// <summary>
        /// current auth token, from the last successfull login.
        /// raw auth token!
        /// </summary>
        string rawToken;

        /// <summary>
        /// current bearer token string, from the last successfull login
        /// </summary>
        string Token
        {
            get
            {
                return "Bearer " + rawToken;
            }
        }

        /// <summary>
        /// initialize the dpx client
        /// </summary>
        /// <param name="host">the hostname of the dpx master server (ex. http://dpx-master.local)</param>
        /// <param name="logRequests">if true, requests and responses are logged using <see cref="HttpLoggingHandler"/></param>
        public DPXClient(string host, bool logRequests = false)
        {
            //set json settings
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings()
            {
                //Date&Time Settings: Use ISO Date format without milliseconds, always convert times to UTC
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                //DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm.fff':'ss'Z'"
            };

            //init dpx api
            if (logRequests)
            {
                //init logger to write to file
                HttpLoggingHandler logger = new HttpLoggingHandler()
                {
                    LogWriter = File.CreateText(DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss.FF") + "_dpx-communication.log")
                };

                //init ReFit to use logger
                HttpClient httpClient = new HttpClient(logger)
                {
                    BaseAddress = new Uri(host),
                };
                dpx = RestService.For<DPXApi>(httpClient);
            }
            else
            {
                dpx = RestService.For<DPXApi>(host);
            }
        }

        /// <summary>
        /// Log into the dpx server
        /// Call this BEFORE using any other function
        /// </summary>
        /// <param name="username">the username to use for login</param>
        /// <param name="password">the (cleartext) password to use for login</param>
        /// <returns>was login successfull?</returns>
        public async Task<bool> LoginAsync(string username, string password)
        {
            //check state first
            ThrowIfInvalidState(false);

            //call api
            LoginResponse response = await dpx.Login(new LoginRequest()
            {
                Username = username,
                Password = password
            });

            //check response is ok
            if (response == null || string.IsNullOrWhiteSpace(response.Token))
                return false;

            //login ok
            rawToken = response.Token;
            return true;
        }

        /// <summary>
        /// Get information about the master server license
        /// </summary>
        /// <returns>the license information</returns>
        public async Task<LicenseResponse> GetLicenseInfoAsync()
        {
            //check state is valid and authentificated
            ThrowIfInvalidState();

            //send request
            LicenseResponse response = await dpx.GetLicense(Token);
            response.SourceClient = this;
            return response;
        }

        /// <summary>
        /// Get a list of job instances that match the given filters
        /// </summary>
        /// <param name="filters">the filters to use.</param>
        /// <returns>the list of found job instances</returns>
        public async Task<JobInstance[]> GetJobInstancesAsync(params FilterItem[] filters)
        {
            //check state+
            ThrowIfInvalidState();

            //send request
            JobInstance[] jobs = await dpx.GetJobInstances(Token, JSONSerialize(filters));

            //set client reference in each job
            foreach (JobInstance job in jobs)
                job.SourceClient = this;

            return jobs;
        }

        /// <summary>
        /// get logs of the job instance with the given id
        /// </summary>
        /// <param name="jobInstanceID">the job instance to get logs of</param>
        /// <param name="startIndex">the index of the first log entry to get</param>
        /// <param name="count">how many entries to get</param>
        /// <param name="filters">filters to apply to the logs. WARNING: this is more inofficial functionality</param>
        /// <returns>the list of log entries found</returns>
        public async Task<InstanceLogEntry[]> GetJobInstanceLogsAsync(long jobInstanceID, long startIndex = 0, long count = 500, params FilterItem[] filters)
        {
            //check state
            ThrowIfInvalidState();

            //get logs
            InstanceLogEntry[] logs = await dpx.GetLogEntries(Token, jobInstanceID, startIndex, count,
                                                                filters.Length == 0 ? null : JSONSerialize(filters));
            foreach (InstanceLogEntry log in logs)
                log.SourceClient = this;

            return logs;
        }

        /// <summary>
        /// Get all logs of the job instance with the given id
        /// </summary>
        /// <param name="jobInstanceID">the job instance to get logs of</param>
        /// <param name="batchSize">how many logs to load at once</param>
        /// <param name="filters">filters to apply to the logs. WARNING: this is more inofficial functionality</param>
        /// <returns>the list of all log entries found</returns>
        public async Task<InstanceLogEntry[]> GetAllJobInstanceLogsAsync(long jobInstanceID, long batchSize = 500, params FilterItem[] filters)
        {
            //check state
            ThrowIfInvalidState();

            //get all logs, 500 at a time
            List<InstanceLogEntry> logs = new List<InstanceLogEntry>();
            InstanceLogEntry[] currentBatch;
            do
            {
                currentBatch = await GetJobInstanceLogsAsync(jobInstanceID, logs.Count, batchSize, filters);
                logs.AddRange(currentBatch);
            } while (currentBatch.Length >= batchSize);
            return logs.ToArray();
        }

        /// <summary>
        /// Get the job status info from a status url
        /// </summary>
        /// <param name="statusURL">the status url</param>
        /// <returns>the status info</returns>
        public async Task<JobStatusInfo> GetStatusInfoAsync(string statusURL)
        {
            //check state
            ThrowIfInvalidState();

            //parse status string from url (last path segment)
            if (!Uri.TryCreate(statusURL, UriKind.Absolute, out Uri result))
                return null;

            string statusSegment = result.Segments.Last();

            //invoke api
            return await dpx.GetStatusInfo(statusSegment);
        }

        /// <summary>
        /// throw a exception if the dpx api instance or auth token are not in a valid state
        /// </summary>
        /// <param name="needAuth">if false, state of auth token is not checked</param>
        void ThrowIfInvalidState(bool needAuth = true)
        {
            //check dpx api
            if (dpx == null)
                throw new InvalidOperationException("dpx api was not initialized!");

            //check access token
            if (needAuth && string.IsNullOrWhiteSpace(rawToken))
                throw new InvalidOperationException("auth token is not set! use DPXClient.Login first.");
        }

        /// <summary>
        /// Serialize a object into a json string using <see cref="JsonConvert"/>
        /// </summary>
        /// <typeparam name="T">the type to convert</typeparam>
        /// <param name="obj">the object to convert</param>
        /// <returns>the json string</returns>
        string JSONSerialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
    }
}
