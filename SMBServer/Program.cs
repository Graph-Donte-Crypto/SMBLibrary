/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SMBServer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            var ui = new ServerUI();

            ui.chkSMB2.Checked = true;
            ui.chkSMB1.Checked = true;

            var addresses = ServerUI.GetIPAddresses();

            Console.WriteLine("Choose network interface");
            foreach (var address in addresses.Select((x, i) => (x,i)))
            {
                Console.WriteLine(address.i + " " + address.x.Key);
            }
            var str = Console.ReadLine();
            int number = int.Parse(str);

            ui.Start(addresses[number].Value, SMBLibrary.SMBTransportType.NetBiosOverTCP, true);

            Console.WriteLine("--- HEELOOO THEERE ---");
            Console.ReadLine();

            ui.Stop();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject != null)
            {
                Exception ex = (Exception)e.ExceptionObject;
                string message = String.Format("Exception: {0}: {1} Source: {2} {3}", ex.GetType(), ex.Message, ex.Source, ex.StackTrace);
                Console.WriteLine(message);
            }
        }
    }
}