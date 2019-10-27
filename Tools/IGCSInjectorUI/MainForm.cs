﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;

namespace IGCSInjectorUI
{
    public partial class MainForm : Form
    {
		private Process _selectedProcess;
		private Dictionary<string, string> _recentProcessesWithDllsUsed;		// key: process name (blabla.exe), value: dll name (full path). 
		private string _defaultProcessName;
		private string _defaultDllName;

        public MainForm()
        {
            InitializeComponent();
			_selectedProcess = null;
			_recentProcessesWithDllsUsed = new Dictionary<string, string>();

			LoadDefaultNamesFromConfigFile();
			FindDefaultProcess();

			LoadRecentProcessList();

			DisplayProcessInForm();
			DisplayVersionInForm();
		}

		private void FindDefaultProcess()
		{
			if(string.IsNullOrWhiteSpace(_defaultProcessName))
			{
				return;
			}
			var currentProcess = Process.GetCurrentProcess();
			_selectedProcess = Process.GetProcesses().FirstOrDefault(p=>p.SessionId == currentProcess.SessionId && !string.IsNullOrEmpty(p.MainWindowTitle) && 
																		p.MainModule.ModuleName.ToLowerInvariant()==_defaultProcessName);
		}

		private void LoadDefaultNamesFromConfigFile()
		{
			_defaultDllName = ConfigurationManager.AppSettings["defaultDllName"] ?? string.Empty;
			_defaultProcessName = (ConfigurationManager.AppSettings["defaultProcessName"] ?? string.Empty).ToLowerInvariant();
			if(string.IsNullOrWhiteSpace(_defaultDllName))
			{
				return;
			}
			if(Path.GetExtension(_defaultDllName).ToLowerInvariant()!=".dll")
			{
				_defaultDllName = string.Empty;
				return;
			}
			// check if the default dll name contains a path. 
			var pathInDllName = Path.GetDirectoryName(_defaultDllName);
			if(pathInDllName==null)
			{
				// invalid
				_defaultDllName = string.Empty;
				return;
			}
			if(string.IsNullOrEmpty(pathInDllName))
			{
				_defaultDllName = Path.Combine(Environment.CurrentDirectory, _defaultDllName);
			}
			if(!File.Exists(_defaultDllName))
			{
				_defaultDllName = string.Empty;
				return;
			}
			// all clear. 
			_dllFilenameTextBox.Text = _defaultDllName;
		}

		private void DisplayVersionInForm()
		{
			this.Text += string.Format(" v{0}", this.GetType().Assembly.GetName().Version.ToString(3));
		}

		private void DisplayProcessInForm()
		{
			if(_selectedProcess==null)
			{
				_processNameTextBox.Text = "Please select a process...";
			}
			else
			{
				_processNameTextBox.Text = string.Format("({0}) {1} ({2})", _selectedProcess.Id,  _selectedProcess.MainModule.ModuleName, _selectedProcess.MainWindowTitle);
			}
		}

		private string GetAbsolutePathForDllName()
		{
			var toReturn = _dllFilenameTextBox.Text;
			if(string.IsNullOrWhiteSpace(toReturn))
			{
				return string.Empty;
			}
			if (Path.IsPathRooted(toReturn))
			{
				return toReturn;
			}
			var rawToReturn = Path.Combine(Environment.CurrentDirectory, toReturn);
			return Path.GetFullPath(rawToReturn).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}


		private void EnableDisableInjectButton()
		{
			_injectButton.Enabled = IsReadyToInject();
		}

		private bool IsReadyToInject()
		{
			var selectedFileExtension = (string.IsNullOrWhiteSpace(_dllFilenameTextBox.Text) ? string.Empty : Path.GetExtension(_dllFilenameTextBox.Text)) ?? string.Empty;
			return (_selectedProcess!=null && File.Exists(_dllFilenameTextBox.Text) && selectedFileExtension.ToLowerInvariant()==".dll");
		}

		private void _cancelButton_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		private void _processNameTextBox_TextChanged(object sender, EventArgs e)
		{
			EnableDisableInjectButton();
		}

		private void _dllFilenameTextBox_TextChanged(object sender, EventArgs e)
		{
			EnableDisableInjectButton();
		}

		private void _browseForDllButton_Click(object sender, EventArgs e)
		{
			_openDllToInjectDialog.InitialDirectory = Environment.CurrentDirectory;
			var result = _openDllToInjectDialog.ShowDialog(this);
			if(result==DialogResult.Cancel)
			{
				return;
			}
			_dllFilenameTextBox.Text = _openDllToInjectDialog.FileName;
			_mainToolTip.SetToolTip(_dllFilenameTextBox, _dllFilenameTextBox.Text);
		}

		private void _selectProcessButton_Click(object sender, EventArgs e)
		{
			using(var processSelector = new ProcessSelector(_recentProcessesWithDllsUsed))
			{
				var result = processSelector.ShowDialog(this);
				if(result==DialogResult.Cancel)
				{
					return;
				}
				_selectedProcess = processSelector.SelectedProcess;
				DisplayProcessInForm();
			}
		}

		private void _injectButton_Click(object sender, EventArgs e)
		{
			if(!IsReadyToInject())
			{
				return;
			}
			// assume things are OK
			var injector = new DllInjector();
			var dllAbsolutePath = GetAbsolutePathForDllName();
			var result = injector.PerformInjection(dllAbsolutePath, _selectedProcess.Id);
			if(result)
			{
				MessageBox.Show(this, "Injection succeeded. Enjoy!", "Injection result", MessageBoxButtons.OK, MessageBoxIcon.Information);
				// we can now exit.
				this.Close();
			}
			else
			{
				MessageBox.Show(this, string.Format("Injection failed when performing:{0}{1}{0}The following error occurred:{0}{2}", 
											Environment.NewLine, injector.LastActionPerformed, new Win32Exception(injector.LastError).Message), "Injection result", 
											MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void _aboutButton_Click(object sender, EventArgs e)
		{
			using(var aboutForm = new About())
			{
				aboutForm.ShowDialog(this);
			}
		}
	}
}
