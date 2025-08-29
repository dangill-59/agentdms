using Xunit;
using AgentDMS.Web.Models;

namespace AgentDMS.Tests
{
    public class MistralConfigTimeoutTests
    {
        [Fact]
        public void MistralConfig_ShouldHaveDefaultTimeoutOf300Seconds()
        {
            // Arrange & Act
            var config = new MistralConfig();
            
            // Assert
            Assert.Equal(300, config.TimeoutSeconds);
        }
        
        [Theory]
        [InlineData(30)]
        [InlineData(300)]
        [InlineData(600)]
        [InlineData(1800)]
        public void MistralConfig_ShouldAcceptValidTimeoutValues(int timeoutSeconds)
        {
            // Arrange & Act
            var config = new MistralConfig { TimeoutSeconds = timeoutSeconds };
            
            // Assert
            Assert.Equal(timeoutSeconds, config.TimeoutSeconds);
        }
    }
}