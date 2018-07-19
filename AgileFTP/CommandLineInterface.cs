﻿using System;
using System.IO;
using FtpConnection;

namespace AgileFTP {
    public static class CommandLineInterface {

        private static FtpConnectionManager connection;
        private static bool running;

        public static void Start() {
            Login();
        }

        private static void Login() {
            Console.Write("Enter hostname:");
            String h = Console.ReadLine();
            if (h == "?") {
                SkipLogin();
                return;
            }
            Console.Write("Enter username:");
            String u = Console.ReadLine();
            Console.Write("Enter password:");
            String p = Console.ReadLine();
            connection = new FtpConnectionManager(u, p, h);
            if (connection.Validate())
                ProcessInput();
            else {
                Console.WriteLine("Login Failed");
                Login();
            }
                
        }

        private static void SkipLogin() {
            connection = new FtpConnectionManager();
            if (connection.Validate())
                ProcessInput();
            else {
                Console.WriteLine("Test Login Failed");
            }
        }

        private static void ProcessInput() {
            running = true;
            while (running) {
                Console.Write(">");
                String cmd = Console.ReadLine();
                ParseCommand(cmd);
            }
        }

        private static void ParseCommand(string cmd) {

            string[] args = cmd.ToLower().Split(' ');

            switch (args[0])
            {
                // FTP Options
                case "cd":
                    connection.ChangeDirectory(args[1]);
                    break;
                case "upload":
                    userUploadFile();
                    break;
                case "ls":
                    ListFiles();
                    break;
                default:
                    Console.WriteLine("Command was not found.");
                    break;
            }
        }

        private static void ListFiles()
        {
            Console.WriteLine(@"Directory to list (Eg. /home");
            string path = @"./" + Console.ReadLine();
            string files = connection.listFiles(path);
            Console.WriteLine("{0}", files);
        }

        public static void userUploadFile()
        {
            Console.Write("File to upload (Eg. C:/Users/Frank/something.txt): ");
            string ftpAddress = "ftp://73.180.17.142/";
            string filePath = Console.ReadLine();
            string fileName = Path.GetFileName(filePath);

            connection.Upload(fileName, ftpAddress, filePath);
        }
    }
}
