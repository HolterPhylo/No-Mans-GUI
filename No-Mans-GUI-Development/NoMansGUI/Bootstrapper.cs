﻿using Caliburn.Micro;
using NMGUIFramework.Interfaces;
using NoMansGUI.Utils.Events;
using NoMansGUI.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Composition.ReflectionModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace NoMansGUI
{
    public class Bootstrapper : BootstrapperBase, IHandle<SplashClickEvent>
    {
        private CompositionContainer container;
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private SplashWindowViewModel _vm;

        public Bootstrapper()
        {
            Initialize(); 
        }

        /// <summary>
        /// If we want to handle anything when the application exits, this would be where it's added.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnExit(object sender, EventArgs e)
        {
            base.OnExit(sender, e);
        }

        /// <summary>
        /// A good catch all for unhandled exceptions, although we should obviously try and handle them!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            m_log.Fatal(string.Format("Unhandled Exception - {0}", e.Exception.Message));
            base.OnUnhandledException(sender, e);
        }

        protected override void Configure()
        {
            var catalog = new AggregateCatalog(AssemblySource.Instance.Select(x => new AssemblyCatalog(x)).OfType<ComposablePartCatalog>());

            //TODO: Move this into config to allow a dynamic folder?
            var pluginFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginFolder))
            {
                Directory.CreateDirectory(pluginFolder);
            }

            catalog.Catalogs.Add(new DirectoryCatalog(pluginFolder));
            container = new CompositionContainer(catalog);
            CompositionBatch batch = new CompositionBatch();
            IEventAggregator eventAggregator = new EventAggregator();
            eventAggregator.Subscribe(this);
            batch.AddExportedValue<IWindowManager>(new WindowManager());
            batch.AddExportedValue<IEventAggregator>(eventAggregator);
            batch.AddExportedValue(container);

            container.Compose(batch);
        }

        /// <summary>
        /// Borrowed this from one of my work apps, it's a good example of using the background worker to handle loading while not locking
        /// up the UI. If you dig into splashscreen a little, it also has a good example of CB events and how easy they are to use.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ev"></param>
        protected override void OnStartup(object sender, System.Windows.StartupEventArgs ev)
        {
            try
            {
                //Create instance of the background worker, we only need it locally.
                var bw = new BackgroundWorker();

                _vm = new SplashWindowViewModel();
                //Tell the background where what to actually do, we could split this into another method, but it's fine here for now.
                bw.DoWork += (xs, args) =>
                {
                    //Just some basic logging using log4net.
                    m_log.Error("Starting Application");
                    //This is an example of caliburns events, we publish an event on the UI thread which is then handled by other classes.
                    //in this case it's handled by the splashscreenviewmodel.
                    IoC.Get<IEventAggregator>().PublishOnUIThread(new LoadingStatusMessage("Beginning Application Setup"));

                    if (Properties.Settings.Default.CallUpgrade)
                    {
                        Properties.Settings.Default.Upgrade();
                        Properties.Settings.Default.CallUpgrade = false;
                    }

                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                    // Combine the base folder with your specific folder....
                    string specificFolder = Path.Combine(folder, "NoMansGUI");

                    // Check if folder exists and if not, create it
                    if (!Directory.Exists(specificFolder))
                    {
                        Directory.CreateDirectory(specificFolder);
                    }

                    m_log.Info("Starting Data Processing");
                
                };

                //Tell the background worker what to do when we've finished DoWork.
                bw.RunWorkerCompleted += (s, args) =>
                {
                    //Ensure we didn't get any errors in DoWork.
                    if (args.Error != null)
                    {
                        //Log any errors we did, not exactly handled well, but it's fine for now.
                        m_log.Error(args.Error.Message);
                    }
                    IoC.Get<IEventAggregator>().PublishOnUIThread(new LoadingCompletedEvent());
                    
                    base.OnStartup(sender, ev);
                };

                //We can now actually start the background worker.
                bw.RunWorkerAsync(); // starts the background worker
                //And display the fancy little animated splashscreen
                IoC.Get<IWindowManager>().ShowWindow(_vm);
            }
            catch (Exception ex)
            {
                m_log.Error(ex);
            }
        }


        protected override IEnumerable<Assembly> SelectAssemblies()
        {
            string path = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);
            return base.SelectAssemblies().Concat(
                  new Assembly[]
                  {
                      //Specifically load the NMGUIFramework.dll because i can't work out how to do it dynamically and i'm too lazy to solve it atm.
                    Assembly.LoadFile(Path.Combine(path, "NMGUIFramework.dll"))
                  });
        }

        private static DirectoryInfo GetExecutingDirectory()
        {
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            return new FileInfo(location.AbsolutePath).Directory;
        }


        protected override object GetInstance(Type service, string key)
        {
            try
            {
                string contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(service) : key;
                var exports = container.GetExports<object>(contract);

                if (exports.Any())
                    return exports.First().Value;

                throw new Exception(string.Format("Could not locate any instances of contract {0}.", contract));
            }
            catch (Exception ex)
            {
                string e = ex.Message;
                return null;
            }
        }

        protected override IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return container.GetExportedValues<object>(AttributedModelServices.GetContractName(serviceType));
        }

        protected override void BuildUp(object instance)
        {
            container.SatisfyImportsOnce(instance);
        }

        public void Dispose()
        {
            container.Dispose();
        }

        public void Handle(SplashClickEvent message)
        {
            if(Properties.Settings.Default.pathUnpakdFiles == "blank")
            {
                MessageBoxResult box = MessageBox.Show("You'll need to add the locations of your Unpakd Files and PCBANKs directory.");
                IoC.Get<IWindowManager>().ShowDialog(new SettingsViewModel());
                _vm.TryClose();
            }

            //Display the root view, in this case it's mainwindowviewmodel, the view will be found automagically.
            try
            {
                DisplayRootViewFor<IShell>();
            }
            catch(Exception ex)
            {
                string e = ex.Message;
            }

            //Close the splashscreen
            _vm.TryClose();
            //Case base startup.
        }
    }
}
