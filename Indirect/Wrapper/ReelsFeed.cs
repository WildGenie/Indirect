﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Indirect.Utilities;
using InstagramAPI;
using InstagramAPI.Classes.Story;

namespace Indirect.Wrapper
{
    class ReelsFeed : IDisposable
    {
        public readonly ObservableCollection<Reel> Reels = new ObservableCollection<Reel>();

        private CancellationTokenSource _reelsUpdateLoop;
        private bool _justUpdated;

        public async Task UpdateReelsFeed()
        {
            if (_justUpdated) return;
            var result = await Instagram.Instance.GetReelsTrayFeed();
            if (!result.IsSucceeded) return;
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SyncReels(result.Value);
            });
            _justUpdated = true;
            _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(x => { _justUpdated = false; });
        }

        private void SyncReels(Reel[] target)
        {
            if (target.Length == 0) return;

            // Remove existing reels that are not in the target
            for (int i = 0; i < Reels.Count; i++)
            {
                var existingReel = Reels[i];
                if (target.All(x => x.Id != existingReel.Id))
                {
                    Reels.RemoveAt(i);
                    i--;
                }
            }

            // Add new reels from target and also update existing ones
            for (int i = 0; i < target.Length; i++)
            {
                var reel = target[i];
                Reel equivalent = null;
                var equivalentIndex = -1;
                for (int j = 0; j < Reels.Count; j++)
                {
                    if (Reels[j].Id == reel.Id)
                    {
                        equivalent = Reels[j];
                        equivalentIndex = j;
                        break;
                    }
                }
                if (equivalent != null)
                {
                    PropertyCopier<Reel, Reel>.Copy(reel, equivalent);
                    if (i != equivalentIndex)
                    {
                        Reels.RemoveAt(equivalentIndex);
                        Reels.Insert(i, equivalent);
                    }
                }
                else
                {
                    Reels.Insert(i, reel);
                }
            }
        }

        public async Task<ReelsWrapper> PrepareReelsWrapper(int selectedIndex)
        {
            var reelsWrapper = new ReelsWrapper(Reels, selectedIndex);
            await reelsWrapper.UpdateUserIndex(selectedIndex);
            return reelsWrapper;
        }

        public async void StartReelsFeedUpdateLoop()
        {
            _reelsUpdateLoop?.Cancel();
            _reelsUpdateLoop?.Dispose();
            _reelsUpdateLoop = new CancellationTokenSource();
            while (!_reelsUpdateLoop.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), _reelsUpdateLoop.Token);
                    await UpdateReelsFeed();
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        public void StopReelsFeedUpdateLoop()
        {
            _reelsUpdateLoop?.Cancel();
            _reelsUpdateLoop?.Dispose();
        }

        public async Task ReplyToStory(StoryItemWrapper story, string message)
        {
            var userId = long.Parse(story.Owner.Id);
            var resultThread = await Instagram.Instance.CreateGroupThreadAsync(new []{userId});
            if (!resultThread.IsSucceeded) return;
            var thread = resultThread.Value;
            var mediaId = story.Id + "_" + story.Owner.Id;
            await Instagram.Instance.SendReelShareAsync(story.Owner.Id, mediaId, thread.ThreadId, message);
        }

        public void Dispose()
        {
            _reelsUpdateLoop?.Cancel();
            _reelsUpdateLoop?.Dispose();
        }
    }
}
