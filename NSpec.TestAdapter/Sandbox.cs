﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NSpec.TestAdapter
{
	/// <summary>
	/// Isolates code execution into separate AppDomain.
	/// </summary>
	/// <typeparam name="T">A class that contain the code that should run isolated.</typeparam>
	class Sandbox<T> : IDisposable where T : DomainProxy
	{
		private AppDomain domain;

		/// <summary>
		/// Initializes a new Sandbox class and loads an assembly into it.
		/// </summary>
		/// <param name="assemblyPath">An assembly to load into the new AppDomain.</param>
		public Sandbox(string assemblyPath)
		{
			var assemblyDirectory = new DirectoryInfo(Path.GetDirectoryName(assemblyPath));
			var projectDirectory = assemblyDirectory.Parent.Parent;
			var solutionDirectory = FindSolutionDirectory(projectDirectory);
			// Import the <assembly>.dll.app.config into the app domain
			var appConfigFile = assemblyPath + ".config";
			var setup = new AppDomainSetup
			{
				ShadowCopyFiles = "true",
				LoaderOptimization = LoaderOptimization.MultiDomain,
				ApplicationBase = solutionDirectory,
				ConfigurationFile = System.IO.File.Exists(appConfigFile) ? appConfigFile : null,
				PrivateBinPath = string.Join(";",
					FindNSpec(projectDirectory),
					assemblyDirectory.FullName.Remove(0, solutionDirectory.Length + 1))
			};
			this.domain = AppDomain.CreateDomain("tests-sandbox", null, setup);

			var type = typeof(T);
			this.Content = this.domain.CreateInstanceFromAndUnwrap(type.Assembly.Location, type.FullName) as T;
			this.Content.Load(assemblyPath);
		}

		/// <summary>
		/// A sandboxed object.
		/// </summary>
		public T Content { get; private set; }

		public void Dispose()
		{
			this.Content = null;
			AppDomain.Unload(domain);
		}

		private string FindSolutionDirectory(DirectoryInfo projectDirectory)
		{
			return projectDirectory.EnumerateDirectories("packages").Any()
				? projectDirectory.FullName
				: FindSolutionDirectory(projectDirectory.Parent);
		}

		private string FindNSpec(DirectoryInfo projectDirectory)
		{
			var packagesFile = Path.Combine(projectDirectory.FullName, "packages.config");
			if (!File.Exists(packagesFile)) return null;

			var doc = XDocument.Load(packagesFile);
			var nspecVersion = doc.Descendants("package")
				.Where(p => p.Attribute("id").Value == "nspec")
				.Select(p => p.Attribute("version").Value)
				.SingleOrDefault();

			if (nspecVersion == null) return null;

			return string.Format(@"packages\nspec.{0}\tools;{1}\packages\nspec.{0}\tools", nspecVersion, projectDirectory.Name);
		}
	}
}
