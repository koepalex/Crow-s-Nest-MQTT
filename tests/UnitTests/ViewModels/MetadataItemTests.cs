using Xunit;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Models;
using System;
using System.ComponentModel;

namespace UnitTests.ViewModels
{
    public class MetadataItemTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var item = new MetadataItem("foo", "bar");
            Assert.Equal("foo", item.Key);
            Assert.Equal("bar", item.Value);
            Assert.Null(item.IconViewModel);
            Assert.False(item.HasIcon);
        }

        [Fact]
        public void Constructor_WithIconViewModel_SetsIcon()
        {
            var iconVm = new ResponseIconViewModel
            {
                RequestMessageId = "msg-123",
                Status = ResponseStatus.Pending
            };

            var item = new MetadataItem("foo", "bar", iconVm);

            Assert.Equal("foo", item.Key);
            Assert.Equal("bar", item.Value);
            Assert.NotNull(item.IconViewModel);
            Assert.Equal("msg-123", item.IconViewModel.RequestMessageId);
        }

        [Fact]
        public void Constructor_WithEmptyStrings_Works()
        {
            var item = new MetadataItem("", "");
            Assert.Equal("", item.Key);
            Assert.Equal("", item.Value);
        }

        [Fact]
        public void Equality_Works()
        {
            var a = new MetadataItem("k", "v");
            var b = new MetadataItem("k", "v");
            var c = new MetadataItem("k", "other");

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void Equality_WithNull_ReturnsFalse()
        {
            var item = new MetadataItem("k", "v");
            Assert.False(item.Equals(null));
        }

        [Fact]
        public void Equality_WithDifferentType_ReturnsFalse()
        {
            var item = new MetadataItem("k", "v");
            Assert.False(item.Equals("not a metadata item"));
            Assert.False(item.Equals(42));
        }

        [Fact]
        public void Equality_IgnoresIconViewModel()
        {
            var icon1 = new ResponseIconViewModel { RequestMessageId = "msg-1" };
            var icon2 = new ResponseIconViewModel { RequestMessageId = "msg-2" };

            var a = new MetadataItem("k", "v", icon1);
            var b = new MetadataItem("k", "v", icon2);

            // Equality should only consider Key and Value, not IconViewModel
            Assert.Equal(a, b);
        }

        [Fact]
        public void GetHashCode_SameForEqualItems()
        {
            var a = new MetadataItem("k", "v");
            var b = new MetadataItem("k", "v");

            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentForDifferentItems()
        {
            var a = new MetadataItem("k", "v1");
            var b = new MetadataItem("k", "v2");

            Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void ToString_ContainsKeyAndValue()
        {
            var item = new MetadataItem("k", "v");
            var str = item.ToString();
            Assert.Contains("k", str);
            Assert.Contains("v", str);
        }

        [Fact]
        public void ToString_ContainsMetadataItemText()
        {
            var item = new MetadataItem("k", "v");
            var str = item.ToString();
            Assert.Contains("MetadataItem", str);
        }

        [Fact]
        public void HasIcon_WithNullIconViewModel_ReturnsFalse()
        {
            var item = new MetadataItem("k", "v");
            Assert.False(item.HasIcon);
        }

        [Fact]
        public void HasIcon_WithVisibleIcon_ReturnsTrue()
        {
            var iconVm = new ResponseIconViewModel
            {
                RequestMessageId = "msg-123",
                IsVisible = true
            };

            var item = new MetadataItem("k", "v", iconVm);
            Assert.True(item.HasIcon);
        }

        [Fact]
        public void HasIcon_WithInvisibleIcon_ReturnsFalse()
        {
            var iconVm = new ResponseIconViewModel
            {
                RequestMessageId = "msg-123",
                IsVisible = false
            };

            var item = new MetadataItem("k", "v", iconVm);
            Assert.False(item.HasIcon);
        }

        [Fact]
        public void IconViewModel_CanBeSet()
        {
            var item = new MetadataItem("k", "v");
            var iconVm = new ResponseIconViewModel { RequestMessageId = "msg-123" };

            item.IconViewModel = iconVm;

            Assert.NotNull(item.IconViewModel);
            Assert.Equal("msg-123", item.IconViewModel.RequestMessageId);
        }

        [Fact]
        public void IconViewModel_RaisesPropertyChanged()
        {
            var item = new MetadataItem("k", "v");
            var iconVm = new ResponseIconViewModel { RequestMessageId = "msg-123" };

            var propertyChangedRaised = false;
            ((INotifyPropertyChanged)item).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MetadataItem.IconViewModel))
                    propertyChangedRaised = true;
            };

            item.IconViewModel = iconVm;

            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void HasIcon_UpdatesWhenIconViewModelVisibilityChanges()
        {
            var iconVm = new ResponseIconViewModel
            {
                RequestMessageId = "msg-123",
                IsVisible = true
            };

            var item = new MetadataItem("k", "v", iconVm);
            Assert.True(item.HasIcon);

            // Change visibility
            iconVm.IsVisible = false;
            Assert.False(item.HasIcon);
        }
    }

    public class ResponseIconViewModelTests
    {
        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            var vm = new ResponseIconViewModel();

            Assert.Equal(string.Empty, vm.RequestMessageId);
            Assert.Equal(ResponseStatus.Hidden, vm.Status);
            Assert.Equal(string.Empty, vm.IconPath);
            Assert.Equal(string.Empty, vm.ToolTip);
            Assert.False(vm.IsClickable);
            Assert.True(vm.IsVisible);
            Assert.Null(vm.NavigationCommand);
        }

        [Fact]
        public void Status_CanBeSet()
        {
            var vm = new ResponseIconViewModel();
            vm.Status = ResponseStatus.Received;
            Assert.Equal(ResponseStatus.Received, vm.Status);
        }

        [Fact]
        public void Status_RaisesPropertyChanged()
        {
            var vm = new ResponseIconViewModel();
            var propertyChangedCount = 0;

            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ResponseIconViewModel.Status))
                    propertyChangedCount++;
            };

            vm.Status = ResponseStatus.Received;
            Assert.Equal(1, propertyChangedCount);
        }

        [Fact]
        public void IsResponseReceived_ReturnsTrue_WhenStatusIsReceived()
        {
            var vm = new ResponseIconViewModel { Status = ResponseStatus.Received };
            Assert.True(vm.IsResponseReceived);
        }

        [Fact]
        public void IsResponseReceived_ReturnsFalse_WhenStatusIsNotReceived()
        {
            var vm = new ResponseIconViewModel { Status = ResponseStatus.Pending };
            Assert.False(vm.IsResponseReceived);

            vm.Status = ResponseStatus.Hidden;
            Assert.False(vm.IsResponseReceived);

            vm.Status = ResponseStatus.NavigationDisabled;
            Assert.False(vm.IsResponseReceived);
        }

        [Fact]
        public void Status_Change_RaisesIsResponseReceivedPropertyChanged()
        {
            var vm = new ResponseIconViewModel();
            var propertyChangedRaised = false;

            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ResponseIconViewModel.IsResponseReceived))
                    propertyChangedRaised = true;
            };

            vm.Status = ResponseStatus.Received;

            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void IconPath_CanBeSet()
        {
            var vm = new ResponseIconViewModel();
            vm.IconPath = "/path/to/icon.svg";
            Assert.Equal("/path/to/icon.svg", vm.IconPath);
        }

        [Fact]
        public void IconPath_RaisesPropertyChanged()
        {
            var vm = new ResponseIconViewModel();
            var propertyChangedRaised = false;

            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ResponseIconViewModel.IconPath))
                    propertyChangedRaised = true;
            };

            vm.IconPath = "/path/to/icon.svg";
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void ToolTip_CanBeSet()
        {
            var vm = new ResponseIconViewModel();
            vm.ToolTip = "Click to navigate";
            Assert.Equal("Click to navigate", vm.ToolTip);
        }

        [Fact]
        public void ToolTip_RaisesPropertyChanged()
        {
            var vm = new ResponseIconViewModel();
            var propertyChangedRaised = false;

            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ResponseIconViewModel.ToolTip))
                    propertyChangedRaised = true;
            };

            vm.ToolTip = "New tooltip";
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void IsClickable_CanBeSet()
        {
            var vm = new ResponseIconViewModel();
            vm.IsClickable = true;
            Assert.True(vm.IsClickable);

            vm.IsClickable = false;
            Assert.False(vm.IsClickable);
        }

        [Fact]
        public void IsClickable_RaisesPropertyChanged()
        {
            var vm = new ResponseIconViewModel();
            var propertyChangedRaised = false;

            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ResponseIconViewModel.IsClickable))
                    propertyChangedRaised = true;
            };

            vm.IsClickable = true;
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void IsVisible_CanBeSet()
        {
            var vm = new ResponseIconViewModel();
            vm.IsVisible = false;
            Assert.False(vm.IsVisible);

            vm.IsVisible = true;
            Assert.True(vm.IsVisible);
        }

        [Fact]
        public void IsVisible_RaisesPropertyChanged()
        {
            var vm = new ResponseIconViewModel();
            var propertyChangedRaised = false;

            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ResponseIconViewModel.IsVisible))
                    propertyChangedRaised = true;
            };

            vm.IsVisible = false;
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void NavigationCommand_CanBeSet()
        {
            var vm = new ResponseIconViewModel();
            vm.NavigationCommand = ":gotoresponse msg-123";
            Assert.Equal(":gotoresponse msg-123", vm.NavigationCommand);
        }

        [Fact]
        public void LastUpdated_CanBeSet()
        {
            var vm = new ResponseIconViewModel();
            var testTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            vm.LastUpdated = testTime;
            Assert.Equal(testTime, vm.LastUpdated);
        }

        [Fact]
        public void LastUpdated_DefaultsToUtcNow()
        {
            var before = DateTime.UtcNow;
            var vm = new ResponseIconViewModel();
            var after = DateTime.UtcNow;

            Assert.True(vm.LastUpdated >= before);
            Assert.True(vm.LastUpdated <= after);
        }

        [Fact]
        public void RequestMessageId_CanBeInitialized()
        {
            var vm = new ResponseIconViewModel { RequestMessageId = "msg-456" };
            Assert.Equal("msg-456", vm.RequestMessageId);
        }
    }
}
