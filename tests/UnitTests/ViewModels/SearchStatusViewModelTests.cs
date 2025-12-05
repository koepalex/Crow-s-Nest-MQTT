using System;
using System.Collections.Generic;
using CrowsNestMQTT.BusinessLogic.Navigation;
using CrowsNestMqtt.UI.ViewModels;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class SearchStatusViewModelTests
    {
        [Fact]
        public void UpdateFromContext_NullContext_HidesStatusIndicators()
        {
            // Arrange
            var viewModel = new SearchStatusViewModel();
            var matches = new List<TopicReference>
            {
                new TopicReference("sensor/temperature", "Temperature Sensor", Guid.NewGuid())
            };
            var activeContext = new SearchContext("sensor", matches);

            // Act
            viewModel.UpdateFromContext(activeContext); // ensure we transition from visible state
            viewModel.UpdateFromContext(null);

            // Assert
            Assert.False(viewModel.IsVisible);
            Assert.Equal(string.Empty, viewModel.StatusText);
        }
    }
}
