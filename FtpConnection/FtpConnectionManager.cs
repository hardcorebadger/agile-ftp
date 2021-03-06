﻿using System;
using System.Net;
using System.IO;
using System.Collections.Generic;

namespace FtpConnection
{
    /*
    This class keeps track of a file's details
    */
    class FileDetails {
        public String Mode;
        public String Unknown;
        public String User;
        public String Group;
        public String Size;
        public String Month;
        public String Day;
        public String Time;
        public String Name;
        /*
        Returns true if the file is a directory and false otherwise
        */
        public bool IsDirectory() {
            try {
                return Mode[0] == 'd';
            }
            catch (IndexOutOfRangeException)
            {
                throw new MissingMemberException("The Mode field cannot be empty!");
            }
        }
    }

    public class FtpConnectionManager
    {
        private string username = "";
        private string password = "";
        private string hostname = "";
        private string cwd = "./";

        private NetworkCredential credentials;

        public FtpConnectionManager(string user = "agile_ftp",
                                    string pass = "gilmore",
                                    string host = "pigs.land")
        {
            username = user;
            password = pass;
            hostname = host;

            credentials = new NetworkCredential(username, password);
        }

        /*
        Using the credentials, run a test request to confirm credentials are correct
        */
        public bool Validate() {
            try {
                FtpWebRequest req = GetNewRequest();
                req.Method = WebRequestMethods.Ftp.ListDirectory;
                WebResponse response = req.GetResponse();
                return true;
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public bool ChangeDirectory(string dir) {
            cwd = PreprocessPath(dir);
            if (!cwd.EndsWith("/", StringComparison.Ordinal))
                cwd += "/";
            return true;
        }

        /*
        This method takes in the filename and path to the file as well as the remote path
        in case it is not root and returns true on success and false on failure
        */
        public bool Upload(string filename, string remotepath, string localpath) {
            try {
                if (IsLocalDirectory(localpath)) {
                    if (!MakeDir(remotepath + "/" + filename)) {
                        return false; // this stops uploading if the remote directory already exists
                    }

                    String[] files = Directory.GetFileSystemEntries(localpath);
                    foreach (String file in files) {
                        Upload(Path.GetFileName(file),
                               remotepath + "/" + filename,
                               file);
                    }

                    return true;
                }
                else {
                    return UploadFile(filename, remotepath, localpath);
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Error: {0}", ex.Message);
                return false;
            }
        }

        private bool UploadFile(string filename,string remotepath, string localpath)
        {
            int offset = (int)GetFilesize(remotepath + "/" + filename);
            try
            {
                /* Create the FTP request */
                FtpWebRequest newRequest = GetNewRequest(remotepath + "/" + filename);

                /* Set ftp properties */
                newRequest.Method = WebRequestMethods.Ftp.UploadFile;
                newRequest.Credentials = new NetworkCredential(username, password);
                newRequest.UseBinary = true;
                newRequest.KeepAlive = true;

                /*Load the file we want into a buffer */
                FileStream uploadFileStream = File.OpenRead(localpath);
                byte[] uploadBuffer = new byte[uploadFileStream.Length];

                uploadFileStream.Read(uploadBuffer, 0, uploadBuffer.Length);
                uploadFileStream.Close();

                /*Upload the file to the server*/
                Stream serverStream = newRequest.GetRequestStream();
                serverStream.Write(uploadBuffer, offset, uploadBuffer.Length);
                serverStream.Close();

                return true;
            }
            catch(FileNotFoundException e)
            {
                Console.WriteLine("\nError: file not found.\n");
                return false;
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error: Something nebulous went wrong\n");
                return false;
            }
        }

        public long GetFilesize(string remotePath)
        {
            try
            {
                FtpWebRequest request = GetNewRequest(remotePath);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                request.Credentials = new NetworkCredential(username, password);
                request.UseBinary = true;
                request.KeepAlive = true;

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                long size = response.ContentLength;
                response.Close();

                return size;
            }
            catch (Exception e)
            {
                return 0;
            }
        }

        /*
        This class takes in the filename to download and the remote path to get it from as
        well as the local path to download it to and returns true on success and false on failure
         */
        public bool Download(string filename, string remotepath, string localpath)
        {
            try
            {
                FileInfo file = new FileInfo(localpath + filename);
                string ftpFullPath = "ftp://" + hostname + remotepath;
                FileStream localfileStream;
                FtpWebRequest request = WebRequest.Create(ftpFullPath) as FtpWebRequest;
                request.Credentials = new NetworkCredential(username, password);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                //check if file has previously been downloaded
                if (file.Exists)
                {
                    request.ContentOffset = file.Length;
                    localfileStream = new FileStream(localpath + filename, FileMode.Append, FileAccess.Write);
                }
                else
                {
                    localfileStream = new FileStream(localpath + filename, FileMode.Create, FileAccess.Write);
                }
                WebResponse response = request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                byte[] buffer = new byte[1024];
                int bytesRead = responseStream.Read(buffer, 0, 1024);
                while (bytesRead != 0)
                {
                    localfileStream.Write(buffer, 0, bytesRead);
                    bytesRead = responseStream.Read(buffer, 0, 1024);
                }
                localfileStream.Close();
                responseStream.Close();
                Console.WriteLine("Successfully Downloaded " + filename + " to " + localpath);
                return true;
            }
            /*When you mess up the remote file path */
            catch(NotSupportedException)
            {
                Console.WriteLine("Remote Path Not Found");
                return false;
            }
            /*When you mess up the local file path */
            catch(DirectoryNotFoundException)
            {
                Console.WriteLine("Local Directory Not Found");
                return false;
            }
            /*When you try to download a file you don't have access to, the file doesn't exist, or the directory doesn't exist)*/
            catch(System.Net.WebException)
            {
                Console.WriteLine("File Unavailable (e.g. File Not Found, No Access)");
                return false;
            }
        }

        /*
        This class takes in the filename to change the file to, the old name and the path and returns
        true on success and false on failure
         */
        public bool Rename(string newFilename, string oldFilename, string remotepath)
        {
            try
            {
                /* set ftpFullPath (e.g. ftp://pigs.land/test/yes.jpg) */
                string ftpFullPath = "ftp://" + hostname + remotepath;

                FtpWebRequest newRequest = (FtpWebRequest)WebRequest.Create(ftpFullPath);
                newRequest.Method = WebRequestMethods.Ftp.Rename;

                /*Send the credentials to the server*/
                newRequest.Credentials = new NetworkCredential(username, password);
                newRequest.RenameTo = newFilename;
                newRequest.GetResponse();
                Console.WriteLine("Successfully Renamed " + oldFilename + " to " + newFilename);
                return true;
            }
            catch(System.Net.WebException)
            {
                Console.WriteLine("File Unavailable (e.g. File Not Found, No Access)");
                return false;
            }
        }

        /*
        This class takes in the filename of the file to delete and the path and
        returns true on success and false on failure
         */
        public bool Delete(string filename, string remotepath)
        {
            try
            {
                var request = GetNewRequest(PreprocessPath(remotepath) + filename);
                request.Method = WebRequestMethods.Ftp.DeleteFile;

                FtpWebResponse response = (FtpWebResponse) request.GetResponse();
		Console.WriteLine("Successfully removed " + filename);
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine("File Unavailable (e.g. File Not Found, No Access)");
                return false;
            }
        }

	/*
        This class takes in the path and
        returns true on success and false on failure
         */
        public bool DeleteDir(string remotepath)
        {
            try
            {
                var request = GetNewRequest(PreprocessPath(remotepath));
                request.Method = WebRequestMethods.Ftp.RemoveDirectory;

                FtpWebResponse response = (FtpWebResponse) request.GetResponse();
		Console.WriteLine("Successfully removed " + remotepath);
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine("File Unavailable (e.g. File Not Found, No Access)");
                return false;
            }
        }

        /*
        I don't know if this should return a bool or if it will need to return something else feel free to change if you take the ticket for it
         */
        public string listFiles(string remotePath)
        {
            try
            {
                /*create a request and set the method to request list directory*/
                FtpWebRequest request = GetNewRequest(remotePath);
                request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                /*create a response and get a response from the request*/
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                /*read stream and convert to string and return*/
                 Stream filesToList = response.GetResponseStream();
                 StreamReader reader = new StreamReader(filesToList);
                return reader.ReadToEnd();
            }
            catch(Exception ex)
            {
                return "could not list files from remote host";
            }
        }

        public bool MakeDir(String remotePath) {
            try {
                FtpWebRequest request = GetNewRequest(remotePath);
                request.Method = WebRequestMethods.Ftp.MakeDirectory;

                request.GetResponse();
                return true;
            }
            catch {
                return false;
            }
        }

        /*
        I don't know what inputs will be needed for this so feel free to add the ones you need if you take the ticket
        */
        public bool ChangePermissions(string filename)
        {
            try
            {
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        /*
        Organizes a directory listing to a convenient form
        */

        private List<FileDetails> GetDirectoryDetails(string rawListing) {
            String[] listings = rawListing.Split('\n');

            List<FileDetails> directoryDetails = new List<FileDetails>();
            for (int i = 0; i < listings.Length; ++i)
            {
                try
                {
                    String[] fieldsStrings = listings[i].Split(' ');
                    List<String> fields = new List<String>(fieldsStrings);
                    fields.RemoveAll(f => f == "");
                    if (fields.Count != 9)
                        throw new MissingFieldException("There should be exactly 9 nonempty fields!");

                    Console.WriteLine("Number of fields: {0}", fields.Count);
                    FileDetails fileDetails = new FileDetails { };
                    fileDetails.Mode = fields[0];
                    fileDetails.Unknown = fields[1];
                    fileDetails.User = fields[2];
                    fileDetails.Group = fields[3];
                    fileDetails.Size = fields[4];
                    fileDetails.Month = fields[5];
                    fileDetails.Day = fields[6];
                    fileDetails.Time = fields[7];
                    fileDetails.Name = fields[8];

                    directoryDetails.Add(fileDetails);
                }
                catch(MissingFieldException) {
                    // Not adding this faulty file
                }
            }

            return directoryDetails;
        }

        private bool IsLocalDirectory(String path) {
            FileAttributes attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
        }

        /*
        Generates a new web request
        */
        private FtpWebRequest GetNewRequest(string path) {
            var request = (FtpWebRequest)WebRequest.Create("ftp://" + hostname + "./"+ PreprocessPath(path));
            request.Credentials = new NetworkCredential(username, password);

            return request;
        }
        private FtpWebRequest GetNewRequest() {
            return GetNewRequest("");
        }

        private string PreprocessPath(string path) {

            // . = cwd
            // .. = cwd -1
            // / = root
            // default = cwd

            string final = "";

            // assume cwd
            if (path.Length == 0 || path == ".") {
                final = cwd;
            }
            // go home
            else if (path == "~")
                final = "./";
            // go back
            else if (path == "..")
                final = CwdMinusOne();
            // explicit cwd
            else if (path.StartsWith("./", StringComparison.Ordinal))
                final = cwd + path.Substring(2);
            // explicit root
            else if (path.StartsWith("/", StringComparison.Ordinal))
                final = path;
            else
                final = cwd + path;


            return final;
        }

        private string CwdMinusOne() {
            string[] split = cwd.Split('/');
            if (split.Length == 0)
                return cwd;
            string final = "";
            for (int i = 0; i < split.Length-2; i++) {
                final += split[i] + "/";
            }
            return final;
        }

        public string GetCWD() {
            return cwd;
        }
    }
}
