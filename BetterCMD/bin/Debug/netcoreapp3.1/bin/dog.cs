using System;

namespace Dog {

    class Program {

        static void Main(string[] args) {

            if (args.Length != 0) {

                foreach (string s in args) {

                    Console.Write(s + " ");

                }

            } else {

                string line;
                while ((line = Console.ReadLine()) != null) {

                    Console.WriteLine(line);

                }

            }

        }

    }

}