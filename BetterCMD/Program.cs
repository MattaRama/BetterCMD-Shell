using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace BetterCMD
{
    class Program
    {
        static void Main(string[] args)
        {

            //Escapes ctrl+C
            //Console.CancelKeyPress += Console_CancelKeyPress;
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
            {

                e.Cancel = true;

            };

            while (true)
            {

                //Gets input
                Console.Write("> ");
                var st = Console.ReadLine();

                //Ctrl+C catch
                if (st == null)
                {
                    Console.WriteLine("^C");
                    continue;
                }

                //Parses command into clear command with arguments
                var cmds = ParseCommands(st);

                //Runs commands, taking into account for piping
                foreach (List<string> t in cmds)
                {

                    //Checks for piping
                    var pipes = ParsePipes(t);

                    //Execute for no pipes, evaluate for pipes
                    if (pipes.Count == 1)
                    {
                        string ex = t[0];
                        t.RemoveAt(0);

                        Execute(ex, ArgsArrToStr(t));

                    } else
                    {

                        try
                        {

                            //Executes pipes
                            string stdOut = "";
                            for (int i = 0; i < pipes.Count; i++)
                            {

                                string ex = pipes[i][0];
                                pipes[i].RemoveAt(0);

                                stdOut = Evaluate(ex, ArgsArrToStr(pipes[i]), stdOut).Trim();

                            }

                            Console.WriteLine(string.Join('\n', stdOut));

                        } catch (Exception e)
                        {
                            Console.WriteLine("Failed to Execute: " + e.Message);
                        }

                    }

                }

            }

        }

        /// <summary>
        /// Converts a list of strings into a single string, separated by spaces
        /// </summary>
        static string ArgsArrToStr(List<string> args)
        {

            if (args.Count == 0)
            {
                return "";
            }

            string ret = "";

            ret += args[0];

            for (int i = 1; i < args.Count; i++)
            {

                ret += " " + args[i];

            }

            return ret;

        }

        /// <summary>
        /// Parses raw input into multiple commands and their arguments
        /// </summary>
        static List<List<string>> ParseCommands(string st)
        {

            //Return list
            var l = new List<List<string>>();

            //For each individual command
            foreach (string t in st.Split(';'))
            {

                string s = t.Trim();

                if (s == "")
                {
                    continue;
                }

                //For constructing Tuple
                var construct = new List<string> { "" };

                bool isQuote = false;

                //For each character
                for (int i = 0; i < s.Length; i++)
                {

                    //Checks for end quote
                    if (s[i] == '"')
                    {
                        isQuote = !isQuote;
                    }

                    //Checks if quoting is currently enabled
                    if (isQuote)
                    {

                        construct[construct.Count - 1] += s[i];
                        continue;

                    }

                    //Checks for space
                    if (s[i] == ' ')
                    {

                        construct.Add("");
                        continue;

                    }

                    //Adds character
                    construct[construct.Count - 1] += s[i];

                }

                l.Add(construct);

            }

            //Returns complete list
            return l;

        }

        /// <summary>
        /// Finds pipes as arguments. If they exist the commands are separated
        /// </summary>
        static List<List<string>> ParsePipes(List<string> st)
        {

            var ret = new List<List<string>>();
            ret.Add(new List<string>());

            foreach (string s_r in st)
            {

                string s = s_r.Trim();

                if (s == "")
                {
                    continue;
                }

                //If pipe is found, split commands
                if (s == "|")
                {

                    ret.Add(new List<string>());
                    continue;

                }

                //Adds to current
                ret[ret.Count - 1].Add(s);

            }

            return ret;

        }

        /// <summary>
        /// Runs an executable in sync, not capturing stdout for piping
        /// </summary>
        static void Execute(string ex, string args)
        {

            //Starts process
            ProcessStartInfo pi = new ProcessStartInfo(ex, args);
            pi.UseShellExecute = false;

            //Contains until EOP
            try
            {
                var p = Process.Start(pi);
                p.WaitForExit();
                p.Close();

            }
            catch (Exception e)
            {

                //Checks for dll
                if (File.Exists(ex + ".dll"))
                {
                    PerformDllRun(Path.GetFullPath(ex), args);
                }
                else if (Path.GetExtension(Path.GetFullPath(ex)) == ".dll")
                {
                    PerformDllRun(Path.GetFullPath(ex), args);

                }

                //Checks for file exist in bin
                else if (Directory.EnumerateFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "bin", ex + ".*").Any())
                {
                    Execute(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + ex, args);
                }

                //Failure
                else
                {
                    Console.WriteLine("Failed to Execute: " + e.Message);
                    //Console.WriteLine("HERE: {0}", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + ex);
                }
            }

        }

        /// <summary>
        /// Runs an executable in sync, capturing stdout and giving designated stdin
        /// </summary>
        static string Evaluate(string ex, string args, string stdin)
        {

            //Starts process
            //Console.WriteLine("STARTING '{0}' WITH ARGS '{1}'", ex, args);
            ProcessStartInfo pi = new ProcessStartInfo(ex, args);
            pi.UseShellExecute = false;
            pi.RedirectStandardOutput = true;
            pi.RedirectStandardInput = true;

            //Starts Process
            try
            {

                var p = Process.Start(pi);

                //Writes stdin
                foreach (char s in stdin)
                {
                    p.StandardInput.Write(s);
                }
                p.StandardInput.Close();

                //Waits for exit
                p.WaitForExit();

                //Returns stdout
                var stdout = p.StandardOutput.ReadToEnd();
                p.Close();
                return stdout;

            } catch (Exception e)
            {
                throw e;
            }

        }

        /// <summary>
        /// Runs a dll file in sync
        /// </summary>
        static void PerformDllRun(string ex, string args)
        {

            try
            {
                //Loads assembly
                Assembly pl = Assembly.LoadFile(ex);

                //Executes every command
                foreach (Type type in pl.GetTypes())
                {

                    //Only allows "Command" classes through
                    if (type.Name != "BetterDll")
                    {
                        continue;
                    }

                    //Creates instance and stores
                    var inst = Activator.CreateInstance(type, args);

                    //Passes Optionals


                    //Runs "Execute()"
                    if (inst.GetType().GetMethod("Execute") != null) type.GetMethod("Execute").Invoke(inst, null);

                }

            } catch (Exception e)
            {
                Console.WriteLine("Failed to Execute: " + e.Message);
            }
            

            

        }
    }
}
