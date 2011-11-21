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
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Security;
using MurrayGrant.ReadablePassphrase.Words;
using MurrayGrant.ReadablePassphrase.WordTemplate;
using MurrayGrant.ReadablePassphrase.PhraseDescription;
using MurrayGrant.ReadablePassphrase.Random;

namespace MurrayGrant.ReadablePassphrase
{
    /// <summary>
    /// Passphrase generator.
    /// </summary>
    public sealed class ReadablePassphraseGenerator
    {
        public WordDictionary Dictionary { get; private set; }
        private RandomSourceBase _Randomness;

        public readonly static Uri CodeplexHomepage = new Uri("http://readablepassphrase.codeplex.com");

        #region Constructor
        /// <summary>
        /// Initialises the object with the default random source (based on <c>RNGCryptoServiceProvider</c>).
        /// </summary>
        public ReadablePassphraseGenerator() 
        { 
            this._Randomness = new CryptoRandomSource();
            this.Dictionary = new WordDictionary();
        }
        /// <summary>
        /// Initialises the object with the given random source.
        /// </summary>
        public ReadablePassphraseGenerator(RandomSourceBase randomness) 
        { 
            this._Randomness = randomness;
            this.Dictionary = new WordDictionary();
        }
        #endregion

        #region LoadDictionary()
        /// <summary>
        /// Loads the default dictionary.
        /// </summary>
        /// <remarks>
        /// This will attempt to load 'dictionary.xml, .xml.gz and .gz' from
        /// the folder of the exe (<c>Assembly.GetEntryAssembly()</c>) or the current directory (<c>Environment.CurrentDirectory</c>).
        /// 
        /// For information about the dictionary schema definition see the default xml file or codeplex website.
        /// </remarks>
        public void LoadDictionary()
        {
            // Check dictionary.xml, dictionary.xml.gz, dictionary.gz in entrypoint and current working directory.
            var filenames = new string[] { "dictionary.xml", "dictionary.xml.gz", "dictionary.gz" };
            var allLocationsToCheck = filenames.Select(f => Path.Combine(System.Reflection.Assembly.GetEntryAssembly().Location, f))
                .Concat(filenames.Select(f => Path.Combine(Environment.CurrentDirectory, f)))
                .ToArray();

            foreach (var fileAndPath in allLocationsToCheck)
            {
                if (TryLoadDictionaryFromPath(fileAndPath))
                    return;
            }

            throw new InvalidOperationException("Unable to load default dictionary. Tried the following locations: " + String.Join(", ", allLocationsToCheck));
        }
        private bool TryLoadDictionaryFromPath(string fileAndPath)
        {
            if (!File.Exists(fileAndPath))
                return false;
            using (var stream = new FileStream(fileAndPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LoadDictionary(stream);
                return true;
            }
        }
        /// <summary>
        /// Loads a dictionary from the specified path.
        /// </summary>
        /// <remarks>
        /// The file can be plaintext or gzipped.
        /// 
        /// For information about the dictionary schema definition see the default xml file or codeplex website.
        /// </remarks>
        public void LoadDictionary(string pathToExternalFile)
        {
            if (!String.IsNullOrEmpty(pathToExternalFile))
                this.LoadDictionary(new FileInfo(pathToExternalFile));
            else
                this.LoadDictionary();
        }
        /// <summary>
        /// Loads a dictionary from the specified file.
        /// </summary>
        /// <remarks>
        /// The file can be plaintext or gzipped.
        /// 
        /// For information about the dictionary schema definition see the default xml file or codeplex website.
        /// </remarks>
        public void LoadDictionary(FileInfo externalFile)
        {
            using (var s = externalFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                LoadDictionary(s);
        }
        /// <summary>
        /// Loads a dictionary from the specified stream.
        /// </summary>
        /// <remarks>
        /// The file can be plaintext or gzipped.
        /// The stream must have <c>CanSeek</c> = true.
        /// 
        /// For information about the dictionary schema definition see the default xml file or codeplex website.
        /// </remarks>
        public void LoadDictionary(Stream s)
        {
            this.Dictionary = new Words.WordDictionary();
            if (!s.CanSeek)
                throw new ArgumentException("Cannot read dictionary from stream which does not support seeking. Use the LoadDictionary(Stream, bool) overload to manually specify compression.", "s");

            // Check to see if the file is compressed or plain text.
            var buf = new byte[2];
            s.Read(buf, 0, buf.Length);
            s.Position = 0;

            if (buf[0] == 0x1f && buf[1] == 0x8b)
            {
                // Found Gzip magic number, decompress before loading.
                var unzipStream = new System.IO.Compression.GZipStream(s, System.IO.Compression.CompressionMode.Decompress);
                this.Dictionary.LoadFrom(unzipStream);
            }
            else
            {
                // Not gziped, assume plaintext.
                this.Dictionary.LoadFrom(s);
            }
        }

        /// <summary>
        /// Loads a dictionary from the specified stream. Use this overload to manually specify if the dictionary is compressed.
        /// </summary>
        /// <param name="s">The stream</param>
        /// <param name="isCompressed">If true, the stream must be compressed, if false it must be uncompressed.</param>
        public void LoadDictionary(Stream s, bool isCompressed)
        {
            this.Dictionary = new Words.WordDictionary();

            if (isCompressed)
            {
                var unzipStream = new System.IO.Compression.GZipStream(s, System.IO.Compression.CompressionMode.Decompress);
                this.Dictionary.LoadFrom(unzipStream);
            }
            else
            {
                this.Dictionary.LoadFrom(s);
            }
        }
        #endregion

        #region CalculateCombinations and Entropy
        /// <summary>
        /// Calculates the number of possible combinations of phrases based on the current dictionary and given phrase strength.
        /// </summary>
        /// <returns>The number of combinations (an integer) as a double (to allow for greater than Int64 combinations)</returns>
        /// <remarks>
        /// This number is a theoretical upper bound assuming no duplicate words in the dictionary.
        /// </remarks>
        public double CalculateCombinations(PhraseStrength strength)
        {
            return this.CalculateCombinations(Clause.CreatePhraseDescription(strength));
        }
        /// <summary>
        /// Calculates the number of possible combinations of phrases based on the current dictionary and given phrase description.
        /// </summary>
        /// <returns>The number of combinations (an integer) as a double (to allow for greater than Int64 combinations)</returns>
        /// <remarks>
        /// This number is a theoretical upper bound assuming no duplicate words in the dictionary.
        /// </remarks>
        public double CalculateCombinations(IEnumerable<Clause> phraseDescription)
        {
            // Multiply all the combinations together.
            if (phraseDescription == null || !phraseDescription.Any())
                return -1;
            return phraseDescription
                    .Select(x => x.CountCombinations(this.Dictionary))
                    .Aggregate((accumulator, next) => accumulator * next);
        }
        /// <summary>
        /// Calculates the number of bits of entropy based on the current dictionary and given phrase strength.
        /// </summary>
        /// <remarks>
        /// This number is based on <c>CalculateCombinations()</c>.
        /// </remarks>
        public double CalculateEntropyBits(PhraseStrength strength)
        {
            return this.CalculateEntropyBits(Clause.CreatePhraseDescription(strength));
        }
        /// <summary>
        /// Calculates the number of bits of entropy based on the current dictionary and given phrase description.
        /// </summary>
        /// <remarks>
        /// This number is based on <c>CalculateCombinations()</c>.
        /// </remarks>
        public double CalculateEntropyBits(IEnumerable<Clause> phraseDescription)
        {
            var combinations = this.CalculateCombinations(phraseDescription);
            return this.CalculateEntropyBits(combinations);
        }
        /// <summary>
        /// Calculates the number of bits of entropy based on the number of combinations.
        /// </summary>
        /// <param name="combinations">As returned from <c>CalculateCombinations()</c>.</param>
        public double CalculateEntropyBits(double combinations)
        {
            if (combinations <= 0)
                return -1;
            return Math.Log(combinations, 2);
        }
        #endregion

        #region GenerateAsSecure()
        /// <summary>
        /// Generates a single phrase as a <c>SecureString</c> based on <c>PasswordStrength.Normal</c>.
        /// </summary>
        public SecureString GenerateAsSecure()
        {
            return GenerateAsSecure(Clause.CreatePhraseDescriptionForNormal(), true);
        }
        /// <summary>
        /// Generates a single phrase as a <c>SecureString</c> based on the given phrase strength.
        /// </summary>
        public SecureString GenerateAsSecure(PhraseStrength strength)
        {
            return GenerateAsSecure(Clause.CreatePhraseDescription(strength), true);
        }
        /// <summary>
        /// Generates a single phrase as a <c>SecureString</c> based on the given phrase description.
        /// </summary>
        public SecureString GenerateAsSecure(IEnumerable<Clause> phraseDescription)
        {
            return GenerateAsSecure(phraseDescription, true);
        }
        /// <summary>
        /// Generates a single phrase as a <c>SecureString</c> based on the given phrase strength.
        /// </summary>
        /// <param name="strength">One of the predefined <c>PhraseStrength</c> enumeration members.</param>
        /// <param name="includeSpacesBetweenWords">Include spaces between words (defaults to true).</param>
        public SecureString GenerateAsSecure(PhraseStrength strength, bool includeSpacesBetweenWords)
        {
            return GenerateAsSecure(Clause.CreatePhraseDescription(strength), includeSpacesBetweenWords);
        }
        /// <summary>
        /// Generates a single phrase as a <c>SecureString</c> based on the given phrase description.
        /// </summary>
        /// <param name="phraseDescription">One or more <c>Clause</c> objects defineing the details of the phrase.</param>
        /// <param name="includeSpacesBetweenWords">Include spaces between words (defaults to true).</param>
        public SecureString GenerateAsSecure(IEnumerable<Clause> phraseDescription, bool includeSpacesBetweenWords)
        {
            if (phraseDescription == null)
                throw new ArgumentNullException("phraseDescription");

            var result = new GenerateInSecureString();
            this.GenerateInternal(phraseDescription, includeSpacesBetweenWords, result);
            if (includeSpacesBetweenWords)
                // When spaces are included between words there is always a trailing space. Remove it.
                result.Target.RemoveAt(result.Target.Length - 1);
            result.Target.MakeReadOnly();
            return result.Target;
        }
        #endregion

        #region Generate()
        /// <summary>
        /// Generates a single phrase based on <c>PasswordStrength.Normal</c>.
        /// </summary>
        public String Generate()
        {
            return Generate(Clause.CreatePhraseDescriptionForNormal());
        }
        /// <summary>
        /// Generates a single phrase based on the given phrase strength.
        /// </summary>
        public String Generate(PhraseStrength strength)
        {
            return Generate(Clause.CreatePhraseDescription(strength), true);
        }
        /// <summary>
        /// Generates a single phrase based on the given phrase strength.
        /// </summary>
        /// <param name="strength">One of the predefined <c>PhraseStrength</c> enumeration members.</param>
        /// <param name="includeSpacesBetweenWords">Include spaces between words (defaults to true).</param>
        public String Generate(PhraseStrength strength, bool includeSpacesBetweenWords)
        {
            return Generate(Clause.CreatePhraseDescription(strength), includeSpacesBetweenWords);
        }
        /// <summary>
        /// Generates a single phrase based on the given phrase description.
        /// </summary>
        public String Generate(IEnumerable<Clause> phraseDescription)
        {
            return Generate(phraseDescription, true);
        }
        /// <summary>
        /// Generates a single phrase based on the given phrase description.
        /// </summary>
        /// <param name="phraseDescription">One or more <c>Clause</c> objects defineing the details of the phrase.</param>
        /// <param name="includeSpacesBetweenWords">Include spaces between words (defaults to true).</param>
        public String Generate(IEnumerable<Clause> phraseDescription, bool includeSpacesBetweenWords)
        {
            if (phraseDescription == null)
                throw new ArgumentNullException("phraseDescription");

            var result = new GenerateInStringBuilder();
            this.GenerateInternal(phraseDescription, includeSpacesBetweenWords, result);
            return result.Target.ToString().Trim();         // A trailing space is always included when spaces are between words.
        }
        #endregion

        #region Internal Generate Methods
        private void GenerateInternal(IEnumerable<Clause> phraseDescription, bool spacesBetweenWords, GenerateTarget result)
        {
            if (phraseDescription == null)
                throw new ArgumentNullException("phraseDescription");
            if (result == null)
                throw new ArgumentNullException("result");
            if (this.Dictionary == null || this.Dictionary.Count == 0)
                throw new InvalidOperationException("You must call LoadDictionary() before any Generate() method.");

            // Build a detailed template by translating the clauses to something which is 1:1 with words. 
            var template = this.PhrasesToTemplate(phraseDescription);

            // Build the phrase based on that template.
            this.TemplateToWords(template, result, spacesBetweenWords);
        }
        private IEnumerable<Template> PhrasesToTemplate(IEnumerable<Clause> phrases)
        {
            // Apply gramatical rules of various kinds.
            var phraseList = phrases.ToList();      // NOTE: Anything more complicated and this will need a tree, possibly a trie.

            // Link NounClauses to VerbClauses.
            // TODO: support more than one verb?? Or zero verbs?
            int verbIdx = phraseList.FindIndex(p => p.GetType() == typeof(VerbClause));
            ((VerbClause)phraseList[verbIdx]).Subject = (NounClause)phraseList[verbIdx - 1];
            ((VerbClause)phraseList[verbIdx]).Object = (NounClause)phraseList[verbIdx + 1];
            

            // Note which noun clause is nominative and accusative.
            foreach (var verb in phraseList.OfType<VerbClause>())
            {
                verb.Subject.IsSubject = true;
                verb.Subject.Verb = verb;
                verb.Object.IsObject = true;
            }


            // Turn the high level phrases into word templates.
            foreach (var subject in phrases.OfType<NounClause>().Where(nc => nc.IsSubject))
            {
                // Generate the subject first because the verb clause plurality depends on it.
                var subjectTemplate = subject.GetWordTemplate(_Randomness);
                subject.Verb.SubjectIsPlural = subjectTemplate.OfType<NounTemplate>().Single().IsPlural;
                foreach (var t in subjectTemplate)
                    yield return t;
                
                // Now for the verb (which knows about its subject's plurality now).
                foreach (var t in subject.Verb.GetWordTemplate(this._Randomness))
                    yield return t;

                // And finally the verb's object.
                foreach (var t in subject.Verb.Object.GetWordTemplate(this._Randomness))
                    yield return t;
            }
        }
        private void TemplateToWords(IEnumerable<Template> template, GenerateTarget target, bool spacesBetweenWords)
        {
            var chosenWords = new HashSet<Word>();
            ArticleTemplate previousArticle = null;
            foreach (var t in template)
            {
                if (t.GetType() == typeof(ArticleTemplate))
                    // Can't directly append an article because it's form depends on the next word (whether it starts with a vowel sound or not).
                    previousArticle = (ArticleTemplate)t;
                else
                {
                    var tuple = t.ChooseWord(this.Dictionary, this._Randomness, chosenWords);
                    if (t.IncludeInAlreadyUsedList)
                        chosenWords.Add(tuple.Word);
                    
                    // Check for a previous article which must be returned before the current word.
                    if (previousArticle != null)
                    {
                        var w = previousArticle.ChooseBasedOnFollowingWord(this.Dictionary, tuple.FinalWord);
                        previousArticle = null;
                        this.AppendWord(target, spacesBetweenWords, w);
                    }

                    // Rather than returning IEnumerable<String>, we build directly into the target (which may be backed by either a StringBuilder or a SecureString).
                    // This interface is required by SecureString as we can only append Char to it and can't easily read its contents back.
                    this.AppendWord(target, spacesBetweenWords, tuple.FinalWord);
                }
            }
        }
        private void AppendWord(GenerateTarget target, bool spacesBetweenWords, string word)
        {
            // Remember that some words in the dictionary are actually multiple words (verbs often have a helper verb with them).
            // So, if there are no spaces, we must remove spaces from words.
            if (spacesBetweenWords)
            {
                target.Append(word);
                target.Append(' ');
            }
            else
            {
                target.Append(word.Replace(" ", ""));
            }

        }

        #endregion
}

    #region Internal GenerateTarget Classes
    internal abstract class GenerateTarget
    {
        public abstract void Append(char c);
        public abstract object Result { get; }
        public void Append(IEnumerable<Char> chars)
        {
            foreach (var c in chars)
                this.Append(c);
        }
    }
    internal class GenerateInStringBuilder : GenerateTarget
    {
        public readonly StringBuilder Target = new StringBuilder();
        public override object Result { get { return this.Target; } }
        public override void Append(char c)
        {
            this.Target.Append(c);
        }
    }
    internal class GenerateInSecureString : GenerateTarget
    {
        public readonly SecureString Target = new SecureString();
        public override object Result { get { return this.Target; } }
        public override void Append(char c)
        {
            this.Target.AppendChar(c);
        }
    }
    #endregion
}
