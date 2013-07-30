﻿// 
// EditorForm.cs
//  
// Author:
//       Peter Cerno <petercerno@gmail.com>
// 
// Copyright (c) 2013 Peter Cerno
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using CodeTag.Common;
using CodeTag.Data;

namespace CodeTag
{
    public partial class EditorForm : Form
    {
        class CodeItem
        {
            public CodeItem()
            {
                
            }

            public CodeItem(XmlCode xmlCode)
            {
                Code = Strip(xmlCode.Code);
                Tags = Strip(xmlCode.Tags);
                Syntax = Strip(xmlCode.Syntax);
            }

            public string Code;
            public string Tags;
            public string Syntax;

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Tags)
                           ? "Code"
                           : string.Format(CultureInfo.InvariantCulture, "Code({0})", Tags);
            }
        }

        class BlockItem
        {
            public BlockItem()
            {
                
            }

            public BlockItem(XmlBlock xmlBlock)
            {
                Name = Strip(xmlBlock.Name);
                Description = Strip(xmlBlock.Description);
                Syntax = Strip(xmlBlock.Syntax);
                Tags = Strip(xmlBlock.Tags);
                Prerequisites = Strip(xmlBlock.Prerequisites);
            }

            public string Name;
            public string Description;
            public string Syntax;
            public string Tags;
            public string Prerequisites;

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Tags)
                           ? string.Format(CultureInfo.InvariantCulture, "[{0}]", Name ?? string.Empty)
                           : string.Format(CultureInfo.InvariantCulture, "[{0}]({1})", Name ?? string.Empty, Tags);
            }
        }

        private const string UntitledFileName = "untitled";
        private const string CaptionSuffix = " - Editor";
        private const string FileNameFilter = "Xml files (*.xml)|*.xml|All files (*.*)|*.*";
        private const string TagSeparator = " ";
        private const string RootBlockName = "Root";
        private const string NewBlockName = "Block";
        private static readonly string PrerequisitesSeparator = Environment.NewLine;
        private static readonly Color EnabledBackColor = SystemColors.Window;
        private static readonly Color DisabledBackColor = SystemColors.ControlDark;

        private string _fileName;
        private bool _isModified;
        private bool _lockLayout;

        private static string Strip(string str)
        {
            return string.IsNullOrWhiteSpace(str) ? null : str.Trim();
        }

        private string FileName
        {
            get { return _fileName; }
            set { _fileName = value; UpdateCaption(); }
        }

        private bool IsModified
        {
            get { return _isModified; }
            set { _isModified = value; UpdateCaption(); }
        }

        private bool LockLayout
        {
            get { return _lockLayout; }
            set
            {
                _lockLayout = value;
                if (_lockLayout) SuspendLayout();
                else ResumeLayout();
            }
        }

        private void UpdateCaption()
        {
            try
            {
                var fileName = string.IsNullOrWhiteSpace(_fileName)
                                   ? UntitledFileName
                                   : Path.GetFileName(_fileName);
                Text = (_isModified ? "*" + fileName : fileName) + CaptionSuffix;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
                // ReSharper disable LocalizableElement
                Text = "Error";
                // ReSharper restore LocalizableElement
            }
        }

        /// <summary>
        /// Delegate type for the event signalizing the change of the code snippet source.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event arguments.</param>
        public delegate void SourceChangedEventHandler(object sender, EventArgs e);

        /// <summary>
        /// Event signalizing the change of the code snippet source.
        /// </summary>
        public event SourceChangedEventHandler SourceChanged;

        protected virtual void OnSourceChanged()
        {
            var handler = SourceChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public EditorForm()
        {
            InitializeComponent();
            NewDocument();
        }

        private void NewDocument()
        {
            LockLayout = true;
            FileName = null;
            IsModified = false;
            treeView.Nodes.Clear();
            var blockItem = new BlockItem
                {
                    Name = RootBlockName
                };
            var rootNode = treeView.Nodes.Add(blockItem.ToString());
            rootNode.Tag = blockItem;
            treeView.SelectedNode = rootNode;
            LockLayout = false;
        }

        private void BuildFromXmlBlock(XmlBlock xmlBlock)
        {
            try
            {
                LockLayout = true;
                IsModified = false;
                treeView.Nodes.Clear();
                var blockItem = new BlockItem(xmlBlock);
                var rootNode = treeView.Nodes.Add(blockItem.ToString());
                rootNode.Tag = blockItem;
                BuildFromXmlBlock(rootNode, xmlBlock);
                treeView.SelectedNode = rootNode;
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
                NewDocument();
            }
        }

        private static void BuildFromXmlBlock(TreeNode node, XmlBlock xmlBlock)
        {
            if (xmlBlock.CodeSnippets != null && xmlBlock.CodeSnippets.Length > 0)
                foreach (var xmlChildCode in xmlBlock.CodeSnippets)
                {
                    var childCodeItem = new CodeItem(xmlChildCode);
                    var childCodeNode = node.Nodes.Add(childCodeItem.ToString());
                    childCodeNode.Tag = childCodeItem;
                }
            if (xmlBlock.Blocks != null && xmlBlock.Blocks.Length > 0)
                foreach (var xmlChildBlock in xmlBlock.Blocks)
                {
                    var childBlockItem = new BlockItem(xmlChildBlock);
                    var childBlockNode = node.Nodes.Add(childBlockItem.ToString());
                    childBlockNode.Tag = childBlockItem;
                    BuildFromXmlBlock(childBlockNode, xmlChildBlock);
                }
        }

        private XmlBlock GetXmlBlock()
        {
            try
            {
                return GetXmlBlock(treeView.Nodes[0]);
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
                return null;
            }
        }

        private static XmlBlock GetXmlBlock(TreeNode node)
        {
            if (!(node.Tag is BlockItem))
                return null;
            var blockItem = node.Tag as BlockItem;
            var xmlBlock = new XmlBlock
                {
                    Name = Strip(blockItem.Name),
                    Syntax = Strip(blockItem.Syntax),
                    Tags = Strip(blockItem.Tags),
                    Description = Strip(blockItem.Description),
                    Prerequisites = Strip(blockItem.Prerequisites)
                };
            var xmlCodeList = new List<XmlCode>();
            var xmlBlockList = new List<XmlBlock>();
            if (node.Nodes.Count > 0)
            {
                foreach (TreeNode childNode in node.Nodes)
                    if (childNode.Tag is CodeItem)
                    {
                        var childCodeItem = childNode.Tag as CodeItem;
                        xmlCodeList.Add(new XmlCode
                            {
                                Syntax = Strip(childCodeItem.Syntax),
                                Tags = Strip(childCodeItem.Tags),
                                Code = Strip(childCodeItem.Code)
                            });
                    }
                    else if (childNode.Tag is BlockItem)
                        xmlBlockList.Add(GetXmlBlock(childNode));
                if (xmlCodeList.Count > 0)
                    xmlBlock.CodeSnippets = xmlCodeList.ToArray();
                if (xmlBlockList.Count > 0)
                    xmlBlock.Blocks = xmlBlockList.ToArray();
            }
            return xmlBlock;
        }

        private DialogResult PromptNewFileName()
        {
            var saveFileDialog = new SaveFileDialog { Filter = FileNameFilter, RestoreDirectory = true };
            var dialogResult = saveFileDialog.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                FileName = saveFileDialog.FileName;
                return DialogResult.OK;
            }
            return dialogResult;
        }

        private bool Save()
        {
            if (string.IsNullOrWhiteSpace(FileName))
                if (PromptNewFileName() != DialogResult.OK)
                    return false;
            try
            {
                var xmlBlock = GetXmlBlock();
                XmlHelper.SerializeToFile(FileName, xmlBlock);
                IsModified = false;
                OnSourceChanged();
                return true;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
                return false;
            }
        }

        private void SaveAs()
        {
            if (PromptNewFileName() == DialogResult.OK)
                Save();
        }

        private DialogResult PromptSave()
        {
            if (IsModified)
            {
                // ReSharper disable LocalizableElement
                var confirmationResult = MessageBox.Show(
                    "Do you want to save the changes?", "Confirmation",
                    MessageBoxButtons.YesNoCancel);
                // ReSharper restore LocalizableElement
                if (confirmationResult == DialogResult.Yes)
                    return Save() ? DialogResult.Yes : DialogResult.Cancel;
                return confirmationResult;
            }
            return DialogResult.Yes;
        }

        private void Open()
        {
            var openFileDialog = new OpenFileDialog {Filter = FileNameFilter, RestoreDirectory = true};
            if (openFileDialog.ShowDialog() != DialogResult.OK) return;
            try
            {
                var xmlBlock = XmlHelper.DeserializeFromFile<XmlBlock>(openFileDialog.FileName);
                if (xmlBlock != null)
                    BuildFromXmlBlock(xmlBlock);
                IsModified = false;
                FileName = openFileDialog.FileName;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }

        private void UpdateSelection()
        {
            try
            {
                var selectedNode = treeView.SelectedNode;
                if (selectedNode == null || selectedNode.Tag == null) return;
                LockLayout = true;
                if (selectedNode.Tag is CodeItem)
                {
                    var codeItem = selectedNode.Tag as CodeItem;
                    nameTextBox.Text = string.Empty;
                    nameTextBox.Enabled = false;
                    nameTextBox.BackColor = DisabledBackColor;
                    descriptionTextBox.Text = string.Empty;
                    descriptionTextBox.Enabled = false;
                    descriptionTextBox.BackColor = DisabledBackColor;
                    syntaxTextBox.Text = codeItem.Syntax ?? string.Empty;
                    syntaxTextBox.Enabled = true;
                    syntaxTextBox.BackColor = EnabledBackColor;
                    tagsTextBox.Text = codeItem.Tags ?? string.Empty;
                    tagsTextBox.Enabled = true;
                    tagsTextBox.BackColor = EnabledBackColor;
                    prerequisitesTextBox.Text = string.Empty;
                    prerequisitesTextBox.Enabled = false;
                    prerequisitesTextBox.BackColor = DisabledBackColor;
                    codeTextBox.Text = codeItem.Code ?? string.Empty;
                    codeTextBox.Enabled = true;
                    codeTextBox.BackColor = EnabledBackColor;
                }
                else if (selectedNode.Tag is BlockItem)
                {
                    var blockItem = selectedNode.Tag as BlockItem;
                    nameTextBox.Text = blockItem.Name ?? string.Empty;
                    nameTextBox.Enabled = true;
                    nameTextBox.BackColor = EnabledBackColor;
                    descriptionTextBox.Text = blockItem.Description ?? string.Empty;
                    descriptionTextBox.Enabled = true;
                    descriptionTextBox.BackColor = EnabledBackColor;
                    syntaxTextBox.Text = blockItem.Syntax ?? string.Empty;
                    syntaxTextBox.Enabled = true;
                    syntaxTextBox.BackColor = EnabledBackColor;
                    tagsTextBox.Text = blockItem.Tags ?? string.Empty;
                    tagsTextBox.Enabled = true;
                    tagsTextBox.BackColor = EnabledBackColor;
                    prerequisitesTextBox.Text = blockItem.Prerequisites ?? string.Empty;
                    prerequisitesTextBox.Enabled = true;
                    prerequisitesTextBox.BackColor = EnabledBackColor;
                    codeTextBox.Text = string.Empty;
                    codeTextBox.Enabled = false;
                    codeTextBox.BackColor = DisabledBackColor;
                }
                inheritedSyntaxTextBox.Text = GetInheritedSyntax(selectedNode.Parent);
                inheritedTagsTextBox.Text = GetInheritedTags(selectedNode.Parent);
                inheritedPrerequisitesTextBox.Text = GetInheritedPrerequisites(selectedNode.Parent);
                var removeButtonEnabled = selectedNode.Parent != null;
                var moveUpButtonEnabled = selectedNode.PrevNode != null && selectedNode.PrevNode.Tag != null &&
                                          selectedNode.Tag.GetType() == selectedNode.PrevNode.Tag.GetType();
                var moveDownButtonEnabled = selectedNode.NextNode != null && selectedNode.NextNode.Tag != null &&
                                            selectedNode.Tag.GetType() == selectedNode.NextNode.Tag.GetType();
                removeButton.Enabled = removeButtonEnabled;
                removeToolStripMenuItem.Enabled = removeButtonEnabled;
                moveUpButton.Enabled = moveUpButtonEnabled;
                moveUpToolStripMenuItem.Enabled = moveUpButtonEnabled;
                moveDownButton.Enabled = moveDownButtonEnabled;
                moveDownToolStripMenuItem.Enabled = moveDownButtonEnabled;
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }

        private static string GetInheritedSyntax(TreeNode node)
        {
            while (node != null)
            {
                if (node.Tag is BlockItem)
                {
                    var blockItem = node.Tag as BlockItem;
                    if (!string.IsNullOrWhiteSpace(blockItem.Syntax))
                        return blockItem.Syntax.Trim();
                }
                node = node.Parent;
            }
            return string.Empty;
        }

        private static string GetInheritedTags(TreeNode node)
        {
            var tagStack = new Stack<string>();
            while (node != null)
            {
                if (node.Tag is BlockItem)
                {
                    var blockItem = node.Tag as BlockItem;
                    if (!string.IsNullOrWhiteSpace(blockItem.Tags))
                        tagStack.Push(blockItem.Tags);
                }
                node = node.Parent;
            }
            return string.Join(TagSeparator, tagStack.ToArray());
        }

        private static string GetInheritedPrerequisites(TreeNode node)
        {
            var prerequisitesStack = new Stack<string>();
            while (node != null)
            {
                if (node.Tag is BlockItem)
                {
                    var blockItem = node.Tag as BlockItem;
                    if (!string.IsNullOrWhiteSpace(blockItem.Prerequisites))
                        prerequisitesStack.Push(blockItem.Prerequisites);
                }
                node = node.Parent;
            }
            return string.Join(PrerequisitesSeparator, prerequisitesStack.ToArray());
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (PromptSave() != DialogResult.Cancel)
                NewDocument();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (PromptSave() != DialogResult.Cancel)
                Open();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAs();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void EditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = IsModified && PromptSave() == DialogResult.Cancel;
        }

        private void addCodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedNode = treeView.SelectedNode;
                if (selectedNode == null) return;
                LockLayout = true;
                IsModified = true;
                var codeItem = new CodeItem();
                if (selectedNode.Tag is CodeItem)
                {
                    var parentNode = selectedNode.Parent;
                    var selectedIndex = selectedNode.Index;
                    var newNode = parentNode.Nodes.Insert(selectedIndex + 1, codeItem.ToString());
                    newNode.Tag = codeItem;
                    treeView.SelectedNode = newNode;
                }
                else if (selectedNode.Tag is BlockItem)
                {
                    int index;
                    for (index = 0; index < selectedNode.Nodes.Count; ++index)
                        if (selectedNode.Nodes[index].Tag is BlockItem) break;
                    var newNode = selectedNode.Nodes.Insert(index, codeItem.ToString());
                    newNode.Tag = codeItem;
                    treeView.SelectedNode = newNode;
                }
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }

        private void addBlockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedNode = treeView.SelectedNode;
                if (selectedNode == null) return;
                LockLayout = true;
                IsModified = true;
                var blockItem = new BlockItem
                {
                    Name = NewBlockName
                };
                if (selectedNode.Tag is CodeItem)
                {
                    var parentNode = selectedNode.Parent;
                    var newNode = parentNode.Nodes.Add(blockItem.ToString());
                    newNode.Tag = blockItem;
                    treeView.SelectedNode = newNode;
                }
                else if (selectedNode.Tag is BlockItem)
                {
                    var newNode = selectedNode.Nodes.Add(blockItem.ToString());
                    newNode.Tag = blockItem;
                    treeView.SelectedNode = newNode;
                }
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedNode = treeView.SelectedNode;
                if (selectedNode == null) return;
                LockLayout = true;
                IsModified = true;
                // ReSharper disable LocalizableElement
                if (selectedNode.Nodes.Count == 0 ||
                    MessageBox.Show("Do you really want to delete this block including all its children?",
                                    "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    var selectNode = selectedNode.NextNode ?? selectedNode.PrevNode ?? selectedNode.Parent;
                    treeView.Nodes.Remove(selectedNode);
                    treeView.SelectedNode = selectNode;
                }
                // ReSharper restore LocalizableElement
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }

        private void moveUpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedNode = treeView.SelectedNode;
                if (selectedNode == null) return;
                LockLayout = true;
                IsModified = true;
                var parentNode = selectedNode.Parent;
                var selectedIndex = selectedNode.Index;
                if (selectedNode.Index == 0) return;
                var clonedNode = (TreeNode)selectedNode.Clone();
                selectedNode.Remove();
                parentNode.Nodes.Insert(selectedIndex - 1, clonedNode);
                parentNode.TreeView.SelectedNode = clonedNode;
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }

        private void moveDownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedNode = treeView.SelectedNode;
                if (selectedNode == null) return;
                LockLayout = true;
                IsModified = true;
                var parentNode = selectedNode.Parent;
                var selectedIndex = selectedNode.Index;
                if (selectedNode.Index == parentNode.Nodes.Count - 1) return;
                var clonedNode = (TreeNode)selectedNode.Clone();
                selectedNode.Remove();
                parentNode.Nodes.Insert(selectedIndex + 1, clonedNode);
                parentNode.TreeView.SelectedNode = clonedNode;
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateSelection();
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (LockLayout) return;
                var selectedNode = treeView.SelectedNode;
                if (selectedNode == null) return;
                LockLayout = true;
                IsModified = true;
                if (selectedNode.Tag is CodeItem)
                {
                    var codeItem = selectedNode.Tag as CodeItem;
                    codeItem.Code = Strip(codeTextBox.Text);
                    codeItem.Syntax = Strip(syntaxTextBox.Text);
                    codeItem.Tags = Strip(tagsTextBox.Text);
                    selectedNode.Text = codeItem.ToString();
                }
                else if (selectedNode.Tag is BlockItem)
                {
                    var blockItem = selectedNode.Tag as BlockItem;
                    blockItem.Name = Strip(nameTextBox.Text);
                    blockItem.Description = Strip(descriptionTextBox.Text);
                    blockItem.Syntax = Strip(syntaxTextBox.Text);
                    blockItem.Tags = Strip(tagsTextBox.Text);
                    blockItem.Prerequisites = Strip(prerequisitesTextBox.Text);
                    selectedNode.Text = blockItem.ToString();
                }
                LockLayout = false;
            }
            catch (Exception exception)
            {
                ErrorReport.Report(exception);
            }
        }
    }
}