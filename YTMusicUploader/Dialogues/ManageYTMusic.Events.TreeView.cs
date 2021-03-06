﻿using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using YTMusicUploader.Providers.RequestModels;

namespace YTMusicUploader.Dialogues
{
    public partial class ManageYTMusic
    {
        private void ScrollUploadsArtistNodeToCentre(TreeNode treeNode)
        {
            treeNode.EnsureVisible();
            if (treeNode.Bounds.Y > (tvUploads.Height / 2))
            {
                if (treeNode.Bounds.Y > (tvUploads.Height / 2))
                {
                    try
                    {
                        if (tvUploads.Nodes[0].Nodes.Count > treeNode.Index + 10)
                        {
                            tvUploads.SelectedNode = tvUploads.Nodes[0].Nodes[treeNode.Index + 10];
                            tvUploads.SelectedNode.EnsureVisible();
                            tvUploads.SelectedNode = null;
                        }
                    }
                    catch { }
                }
            }
        }

        private void TvUploads_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var selectedNode = e.Node;
            if (selectedNode != null)
            {
                foreach (TreeNode artistNode in tvUploads.Nodes[0].Nodes)
                    artistNode.BackColor = Color.White;

                if (((MusicManageTreeNodeModel)selectedNode.Tag).NodeType == MusicManageTreeNodeModel.NodeTypeEnum.Artist)
                {
                    if (selectedNode.Nodes == null || selectedNode.Nodes.Count == 0)
                    {
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            GetAlbums(selectedNode, ((MusicManageTreeNodeModel)selectedNode.Tag).ArtistTitle);
                        });
                    }
                }

                SetMusicDetails((MusicManageTreeNodeModel)selectedNode.Tag);
            }
        }

        private void TvUploads_AfterCheck(object sender, TreeViewEventArgs e)
        {
            CheckAllChildNodes(e.Node, e.Node.Checked);
            if (!e.Node.Checked)
            {
                tvUploads.AfterCheck -= TvUploads_AfterCheck;
                UncheckParentNodes(e.Node);
                tvUploads.AfterCheck += TvUploads_AfterCheck;
            }
            else
            {
                // If all child nodes checked, then check parent node
                tvUploads.AfterCheck -= TvUploads_AfterCheck;
                CheckParentNodes(e.Node);
                tvUploads.AfterCheck += TvUploads_AfterCheck;
            }

            bool nonFetchedArtistAlbumsSelected = false;
            int trackCheckedCount = CountChecked(ref nonFetchedArtistAlbumsSelected);
            lblCheckedCount.Text = $"{trackCheckedCount} track{(trackCheckedCount == 1 ? "" : "s")} checked" +
                                   (nonFetchedArtistAlbumsSelected ? " (non-fetched artist tracks selected)" : "");


            bool artistNodesChecked = false;
            foreach (TreeNode artistNode in tvUploads.Nodes[0].Nodes)
            {
                if (artistNode.Checked)
                {
                    artistNodesChecked = true;
                    break;
                }
            }

            if (trackCheckedCount > 0 || artistNodesChecked)
            {
                PbDeleteYTUploaded.Image = Properties.Resources.delete_from_youtube;
                PbDeleteYTUploaded.Enabled = true;
            }
            else
            {
                PbDeleteYTUploaded.Image = Properties.Resources.delete_from_youtube_disabled;
                PbDeleteYTUploaded.Enabled = false;
            }
        }
    }
}
