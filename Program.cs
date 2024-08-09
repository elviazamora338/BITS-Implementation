using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using BITS = BITSReference10_1;
using System.IO;
using Newtonsoft.Json;
using System.Runtime;




/// <summary>
/// Trying to understand the functionality of BITS Example
/// </summary>
class Program
{
    static int softpaqSize(string url)
    {
        // sends a HEAD request
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        // ensures the request only gets the headers not the full content
        req.Method = "HEAD";
        long len;
        using (HttpWebResponse resp = (HttpWebResponse)(req.GetResponse()))
        {
            // retrieving the softpaq length from the response headers
            len = resp.ContentLength;
        }
        // converting to an int variable
        int bytesTotal = (int)len;

        // returning the total bytes for this softpaq file
        return bytesTotal;
    }

    class BITSJobInfo
    {
        public BITS.GUID jobId { get; set; }
        public string jobName { get; set; }
        public string jobDescription { get; set; }
        public BITS.BG_JOB_STATE jobState { get; set; }
    }

    static void Main(string[] args)
    {
        bool currentJob = false;
        List<BITSJobInfo> jobs;
        string filePath = "BITSJobs.json";
        bool jobCreated = false;
        Console.WriteLine("Checking for existing job file...");
        // list of files to transfer
        // there is a variable in the srl with the urls so this won't be needed
        var filesToTransfer = new List<(string sourceUrl, string destinationPath)>
            {

                 // my largest files
                 // i addede 2 by 2
                //////////////////////////////////////////////////////////////////////////////////////////////////
      
                  // size = 1473515488
                ("https://ftp.hp.com/pub/softpaq/sp142501-143000/sp142669.exe", @"C:\Users\ZaEl706\sp142669.exe"),
                // size is 1500744608
                ("https://ftp.hp.com/pub/softpaq/sp142501-143000/sp142670.exe", @"C:\Users\ZaEl706\sp142670.exe"),

                ("https://ftp.hp.com/pub/softpaq/sp151501-152000/sp151786.exe", @"C:\Users\ZaEl706\sp151786.exe"),

                ("https://ftp.hp.com/pub/softpaq/sp147001-147500/sp147159.exe", @"C:\Users\ZaEl706\sp147159.exe"),
                // size is 1263788968
                ("https://ftp.hp.com/pub/softpaq/sp144501-145000/sp144956.exe", @"C:\Users\ZaEl706\sp144956.exe"),
                
                // size = 1894485360
                ("http://ftp.hp.com/pub/softpaq/sp151501-152000/sp151813.exe",  @"C:\Users\ZaEl706\sp151813.exe"),


                // test files
                ///this was the first approach (all the below)
                //("https://ftp.hp.com/pub/softpaq/sp144501-145000/sp144957.exe", @"c:\users\zael706\sp144957.exe"),
                //("https://ftp.hp.com/pub/softpaq/sp144501-145000/sp144958.exe", @"c:\users\zael706\sp144958.exe"),

                ///////////////////////////////////////////////////////////////////////////////////////////////////////////
                //("http://ftp.hp.com/pub/softpaq/sp149501-150000/sp149691.exe",  @"C:\Users\ZaEl706\sp149691.exe"),
                //("http://ftp.hp.com/pub/softpaq/sp147501-148000/sp147684.exe",  @"C:\Users\ZaEl706\sp147684.exe"),

                //// MOVED THESE
     
                //("http://ftp.hp.com/pub/softpaq/sp153001-153500/sp153045.exe",  @"C:\Users\ZaEl706\sp153045.exe"),

                //("https://ftp.hp.com/pub/softpaq/sp144501-145000/sp144960.exe", @"C:\Users\ZaEl706\sp144960.exe"),

                //// MOVED THESE
                //("https://ftp.hp.com/pub/softpaq/sp142501-143000/sp142668.exe", @"C:\Users\ZaEl706\sp142668.exe"),

            };
        // this will be getting from the file if there is currently a job there
        if (System.IO.File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            // will load from the file
            Console.WriteLine("Loading existing job...");
            jobs = LoadJob(filePath);
            currentJob = true;
        }
        else
        {
            // creating a new
            Console.WriteLine("Creating new job...");
            jobs = InitializeJobInfoList();
            SaveJobInfoList(jobs, filePath);
            Console.WriteLine("saved a new job");
            jobCreated = true;

        }

        Console.WriteLine("Processing jobs...");
        // to hold the list of completed jobs that need to be removed from the JSON file
        List<BITSJobInfo>jobsToRemove = new List<BITSJobInfo>();
        var mgr = new BITS.BackgroundCopyManager10_1();
        BITS.IBackgroundCopyJob job;
        // going through the current list of jobs
        foreach (var jobsInfo in jobs)
        {
            try
            {
                Console.WriteLine($"Processing job with ID: {jobsInfo.jobId}");
              
                // getting current from file
                mgr.GetJob(jobsInfo.jobId, out job);
                // checking the state
                BITS.BG_JOB_STATE jobState;
                job.GetState(out jobState);
                
                jobsInfo.jobState = jobState;

                if (jobCreated == true)
                {
                    Console.WriteLine("Adding files to job");
                    foreach (var file in filesToTransfer)
                    {
                        job.AddFile(file.sourceUrl, file.destinationPath);
                        int v = softpaqSize(file.sourceUrl);
                        Console.WriteLine(v);
                    }
                    job.Resume();
                }


                bool jobIsFinal = false;
                Console.WriteLine("Monitoring job state...");
                // this will basically be checking the state of the transfer
                while (!jobIsFinal)
                {
                    // checking the state every 5 seconds
                    BITS.BG_JOB_STATE state;
                    job.GetState(out state);
                    jobsInfo.jobState = jobState;
                    Console.WriteLine(jobState.ToString());
                    // saving the state to the JSON file
                    SaveJobInfoList(jobs, filePath);
               

                    switch (state)
                    {

                        // if there was no error or stop then it has been completed
                        case BITS.BG_JOB_STATE.BG_JOB_STATE_ERROR:
                        case BITS.BG_JOB_STATE.BG_JOB_STATE_TRANSFERRED:
                            //  complete the job was successful
                            job.Complete();
                            jobIsFinal = true;
                            Console.WriteLine("Job completed successfully.");
                            // add to list of jobs needing to be removed
                            jobsToRemove.Add(jobsInfo);
                            break;
                        // User stopped it or is completed and acknowledged
                        case BITS.BG_JOB_STATE.BG_JOB_STATE_CANCELLED:
                        case BITS.BG_JOB_STATE.BG_JOB_STATE_ACKNOWLEDGED:
                            // leave loop
                            jobIsFinal = true;
                            Console.WriteLine("Job cancelled or acknowledged.");
                            // add to list of jobs needing to be removed
                            jobsToRemove.Add(jobsInfo);
                            break;
                        default:
                            // delay before checking job state again
                            //BITS.BG_JOB_STATE state;
                            job.GetState(out state);
                            Console.WriteLine(jobState.ToString());
                            jobsInfo.jobState = jobState;
                            Task.Delay(500).Wait();

                            break;
                    }
                }

            }
            // an error occurred
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message.ToString());
            }
        }
        // removing from the JSON file
        RemoveFromFile(jobsToRemove, jobs, filePath);

    }

    // loading from the json file
    static List<BITSJobInfo> LoadJob(string filePath)
    {
        List<BITSJobInfo> jobInfoList = new List<BITSJobInfo>();
        try
        {
            // Load job info list from file
            string json = System.IO.File.ReadAllText(filePath);
            jobInfoList = JsonConvert.DeserializeObject<List<BITSJobInfo>>(json);
            Console.WriteLine("Loaded a job from the file");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error loading job: " + ex.Message);
        }
       

        return jobInfoList;
    }
    static void RemoveFromFile(List<BITSJobInfo> jobsToRemove, List<BITSJobInfo> jobs, string filePath)
    {
        // Removing the job from the JSON list
        foreach (var job in jobsToRemove)
        { 
            jobs.RemoveAll(j => j.jobId.Equals(job.jobId));

        }
        if (jobs.Count == 0)
        {
            // If the list is empty, delete the file
            if (System.IO.File.Exists(filePath))
            {
                // deleting the file
                System.IO.File.Delete(filePath);
                Console.WriteLine("List of jobs is empty. The JSON file has been deleted.");
            }
        }
        else
        {
            // save content
            SaveJobInfoList(jobs, filePath);
        }

        Console.WriteLine("\x1B[1;4;92mUpdated the JSON file\x1B[m");
        Console.ReadLine();
    }
    //creating a new job and setting
    static List<BITSJobInfo> InitializeJobInfoList()
    {
        // initialize new job info list
        List<BITSJobInfo> jobInfoList = new List<BITSJobInfo>();


        BITS.GUID id = new BITS.GUID(); // generate a new Job ID
        BITS.IBackgroundCopyJob job;
        var mgr = new BITS.BackgroundCopyManager10_1();
        try
        {
            // creating a new job
            mgr.CreateJob("BITS Transfer", BITS.BG_JOB_TYPE.BG_JOB_TYPE_DOWNLOAD, out id, out job);
            Console.WriteLine($"Job was created with ID: {id}");


            BITS.BG_JOB_STATE jobState;
            job.GetState(out jobState);


            BITSJobInfo jobInfo = new BITSJobInfo
            {
                jobId = id,
                jobName = "BITS Transfer",
                jobDescription = "Downloading file",
                // by default state will be Suspended
                jobState = jobState
            };


            jobInfoList.Add(jobInfo);

        }
        catch (Exception ex)
        {
            Console.WriteLine("Error creating job: " + ex.Message);
        }


        return jobInfoList;
    }


    // will save to the json file
    static void SaveJobInfoList(List<BITSJobInfo> jobInfoList, string filePath)
    {
        // Save job info list to file
        string json = JsonConvert.SerializeObject(jobInfoList, Formatting.Indented);
        System.IO.File.WriteAllText(filePath, json);
    }

}

