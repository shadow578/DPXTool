using DPXTool.DPX.Model.Common;
using DPXTool.DPX.Model.JobInstances;
using DPXTool.DPX.Model.License;
using DPXTool.DPX.Model.Login;
using Refit;
using System.Threading.Tasks;

namespace DPXTool.DPX
{
    /// <summary>
    /// DPX Rest API ReFit interface class
    /// </summary>
    interface DPXApi
    {
        /// <summary>
        /// Login into the DPX Rest api and optain a access token
        /// </summary>
        /// <param name="request">the login request</param>
        /// <returns>the login response</returns>
        [Post("/auth/login")]
        Task<LoginResponse> Login([Body] LoginRequest request);

        /// <summary>
        /// Get license information of the dpx master server
        /// </summary>
        /// <param name="bearerToken">token from Login function. (Bearer {token})</param>
        /// <returns>license information response</returns>
        [Get("/app/api/license")]
        Task<LicenseResponse> GetLicense([Header("Authorization")] string bearerToken);

        /// <summary>
        /// get job instances matching the given filter criteria
        /// </summary>
        /// <param name="bearerToken">token from Login function. (Bearer {token})</param>
        /// <param name="jobFilter">filters for the job instances, serialized array of <see cref="FilterItem"/></param>
        /// <returns>a list of all found job instances</returns>
        [Get("/app/api/job_instances")]
        Task<JobInstance[]> GetJobInstances([Header("Authorization")] string bearerToken, [AliasAs("filter")] string jobFilter);

        /// <summary>
        /// get logs from a job instance with the given instance id
        /// </summary>
        /// <param name="bearerToken">token from Login function. (Bearer {token})</param>
        /// <param name="jobInstanceID">job instange to get logs of</param>
        /// <param name="index">start log entry index</param>
        /// <param name="count">how many log entries to get</param>
        /// <param name="filter">filters to apply to the log entries, serialized array of <see cref="FilterItem"/>. WARNING: this isn't really official api</param>
        /// <returns>a list of all found log entries for this job instance</returns>
        [Get("/app/api/job_instances/{id}/log")]
        Task<InstanceLogEntry[]> GetLogEntries([Header("Authorization")] string bearerToken, [AliasAs("id")] long jobInstanceID, long index, long count, string filter);

        /// <summary>
        /// Get the status info for a status string (eg. COMPLETED)
        /// </summary>
        /// <param name="statusStr">the status string, parsed from status url</param>
        /// <returns>the status info</returns>
        [Get("/app/api/job_instance_statuses/{status}")]
        Task<JobStatusInfo> GetStatusInfo([AliasAs("status")] string statusStr);
    }
}
