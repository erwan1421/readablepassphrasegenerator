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
using System.Linq;
using System.Text;
using MurrayGrant.ReadablePassphrase.Words;

namespace MurrayGrant.ReadablePassphrase.WordTemplate
{
    public class VerbTemplate : Template
    {
        public override bool IncludeInAlreadyUsedList { get { return true; } }
        public readonly bool SubjectIsPlural;
        public readonly VerbTense Tense;

        public VerbTemplate(VerbTense tense, bool subjectIsPlural)
        {
            this.Tense = tense;
            this.SubjectIsPlural = subjectIsPlural;
        }
        public override WordAndString ChooseWord(WordDictionary words, Random.RandomSourceBase randomness, IEnumerable<Word> alreadyChosen)
        {
            var word = words.ChooseWord<Verb>(randomness, alreadyChosen, w => w.HasForm(this.Tense, this.SubjectIsPlural));
            return new WordAndString(word, word.GetForm(this.Tense, this.SubjectIsPlural));
        }
    }

    public enum VerbTense
    {
        Present,
        Past,
        Future,
        Continuous,
        ContinuousPast,
        Perfect,
        Subjunctive
    }
}
