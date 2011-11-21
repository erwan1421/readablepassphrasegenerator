﻿// Copyright 2011 Murray Grant
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MurrayGrant.ReadablePassphrase;

namespace KeePassReadablePassphrase
{
    public partial class ConfigRoot : Form
    {
        public ConfigRoot(string configFromKeePass, MurrayGrant.ReadablePassphrase.Random.RandomSourceBase randomness)
        {
            this.ConfigForKeePass = configFromKeePass;
            this._Generator = new ReadablePassphraseGenerator(randomness);
            InitializeComponent();
        }

        public string ConfigForKeePass { get; private set; }
        private readonly ReadablePassphraseGenerator _Generator;
        private bool IsCurrentPhraseStrengthCustom { get { return (PhraseStrength)Enum.Parse(typeof(PhraseStrength), this.cboPhraseSelection.Text) == PhraseStrength.Custom; } } 

        private void ConfigRoot_Load(object sender, EventArgs e)
        {
            var ver = ((System.Reflection.AssemblyFileVersionAttribute)typeof(ConfigRoot).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyFileVersionAttribute), true).GetValue(0)).Version;
            var idx = ver.IndexOf('.', ver.IndexOf('.')+1);
            this.lblVersion.Text = "Version " + ver.Substring(0, idx);

            // Load up the config.
            Config config = null;
            try
            {
                config = new Config(this.ConfigForKeePass);
            }
            catch (MurrayGrant.ReadablePassphrase.PhraseDescription.PhraseDescriptionParseException ex)
            {
                MessageBox.Show(this, String.Format("Unable to load Readable Passphrase Config.{0}A default configuration will be used instead.{0}{0}{1}{0}{2}", Environment.NewLine, ex.Message, ex.InnerException != null ? ex.InnerException.Message : ""), "Readable Passphrase", MessageBoxButtons.OK, MessageBoxIcon.Error);
                config = new Config();
            }

            // Load the dictionary so we can display the number of words in the config window.
            PassphraseGenerator.LoadDictionary(config, this._Generator);

            // "Data Bind"
            this.ConfigObjectToForm(config);

            this.lblStatus.Text = "";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Save.
            this.ConfigForKeePass = this.FormToConfigObject().ToConfigString();
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private void cboPhraseSelection_SelectedIndexChanged(object sender, EventArgs e)
        {
            // If custom, display the phrase detail and grow the form size.
            var newConf = this.FormToConfigObject();
            this.UpdateDescription(newConf);
            this.UpdateCombinations(newConf);
            this.UpdateCustomStrengthVisibility(this.IsCurrentPhraseStrengthCustom);
        }




        private void lnkWebsite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Browse to codeplex site.
            this.bgwWorker.RunWorkerAsync(ReadablePassphraseGenerator.CodeplexHomepage);            
        }
        private void lnkPhraseHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Browse to doco page for phrases.
            this.bgwWorker.RunWorkerAsync(new Uri("http://readablepassphrase.codeplex.com/wikipage?title=Custom%20Phrase%20Description"));
        }

        private void lnkDictionaryHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Browse to doco page for dictionary definition.
            this.bgwWorker.RunWorkerAsync(new Uri("http://readablepassphrase.codeplex.com/wikipage?title=Make%20Your%20Own%20Dictionary"));
        }

        private void chkCustomDictionary_CheckedChanged(object sender, EventArgs e)
        {
            // If true, enable the textbox and browse button.

        }
        private void btnBrowse_Click(object sender, EventArgs e)
        {

        }
        private void btnDictionarySizeDetail_Click(object sender, EventArgs e)
        {
            using (var frm = new DictionarySizeDetail(this._Generator.Dictionary))
            {
                frm.ShowDialog(this);
            }
        }
        private void txtPhraseDescription_TextChanged(object sender, EventArgs e)
        {
            var conf = this.FormToConfigObject();
            if (conf != null)
                this.UpdateCombinations(conf);
        }



        private void ConfigObjectToForm(Config config)
        {
            this.cboPhraseSelection.DataSource = Enum.GetNames(typeof(PhraseStrength));
            this.cboPhraseSelection.Text = config.PhraseStrength.ToString();
            this.chkSpacesBetweenWords.Checked = config.SpacesBetweenWords;
            this.chkCustomDictionary.Checked = config.UseCustomDictionary;
            this.txtDictionaryPath.Text = config.PathOfCustomDictionary;

            this.UpdateDescription(config);
            this.UpdateCombinations(config);
            this.UpdateDictionarySize(config);
            this.UpdateCustomStrengthVisibility(this.IsCurrentPhraseStrengthCustom);
            this.UpdateCustomDictionaryVisibility();
        }
        private void UpdateDescription(Config config)
        {
            this.txtPhraseDescription.Text = String.Join(Environment.NewLine, config.PhraseDescription.Select(c => c.ToTextString()).ToArray());
        }
        private void UpdateCombinations(Config config)
        {
            var combinations = this._Generator.CalculateCombinations(config.PhraseDescription);
            if (combinations >= 0)
            {
                this.txtCombinations.Text = combinations.ToString("N0");
                this.txtEntropy.Text = this._Generator.CalculateEntropyBits(combinations).ToString("N2") + " bits";
            }
            else
            {
                this.txtCombinations.Text = "?";
                this.txtEntropy.Text = "?";
            }
        }
        private void UpdateDictionarySize(Config config)
        {
            var total = this._Generator.Dictionary.Count;
            this.txtDictionarySize.Text = total.ToString();
        }
        private void UpdateCustomStrengthVisibility(bool isCustomSelected)
        {
            this.lblPhraseDetail.Visible = isCustomSelected;
            this.txtPhraseDescription.Visible = isCustomSelected;
            this.lnkPhraseHelp.Visible = isCustomSelected;

            if (isCustomSelected)
            {
                this.Height = this.txtDictionarySize.Bottom + this.btnOK.Height + 200;
            }
            else
            {
                this.Height = this.txtDictionarySize.Bottom + this.btnOK.Height + 70;
            }
        }
        private void UpdateCustomDictionaryVisibility()
        {
            this.lnkDictionaryHelp.Visible = this.chkCustomDictionary.Checked;
        }
        private Config FormToConfigObject()
        {
            var result = new Config();
            result.PhraseStrength = (PhraseStrength)Enum.Parse(typeof(PhraseStrength), this.cboPhraseSelection.Text);
            result.SpacesBetweenWords = this.chkSpacesBetweenWords.Checked;
            result.UseCustomDictionary = this.chkCustomDictionary.Checked;
            result.PathOfCustomDictionary = this.txtDictionaryPath.Text;
            try
            {
                if (result.PhraseStrength == PhraseStrength.Custom)
                    result.PhraseDescription = MurrayGrant.ReadablePassphrase.PhraseDescription.Clause.CreateCollectionFromTextString(this.txtPhraseDescription.Text);
                this.lblStatus.Text = "";
            }
            catch (MurrayGrant.ReadablePassphrase.PhraseDescription.PhraseDescriptionParseException ex)
            {
                result.PhraseDescription = new MurrayGrant.ReadablePassphrase.PhraseDescription.Clause[0];
                this.lblStatus.Text = ex.Message;
            }
            return result;

        }

        private void bgwWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var url = e.Argument as Uri;
            if (url != null)
            {
                System.Diagnostics.Process.Start(url.ToString());
            }
        }
    }
}
