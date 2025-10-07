using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class CorrelationKeyTests
{
    [Fact]
    public void Constructor_ValidCorrelationData_CreatesInstance()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };

        // Act
        var key = new CorrelationKey(correlationData);

        // Assert
        Assert.NotNull(key.CorrelationData);
        Assert.Equal(correlationData, key.CorrelationData);
    }

    [Fact]
    public void Constructor_NullCorrelationData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CorrelationKey(null!));
    }

    [Fact]
    public void Constructor_EmptyCorrelationData_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CorrelationKey(Array.Empty<byte>()));
    }

    [Fact]
    public void Constructor_CopiesCorrelationData()
    {
        // Arrange
        var originalData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(originalData);

        // Act - Modify original array
        originalData[0] = 99;

        // Assert - Key's data should be unchanged
        Assert.NotEqual(99, key.CorrelationData[0]);
        Assert.Equal(1, key.CorrelationData[0]);
    }

    [Fact]
    public void CorrelationData_ReturnsCopy()
    {
        // Arrange
        var originalData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(originalData);

        // Act - Modify returned array
        var returnedData = key.CorrelationData;
        returnedData[0] = 99;

        // Assert - Key's data should be unchanged
        Assert.NotEqual(99, key.CorrelationData[0]);
        Assert.Equal(1, key.CorrelationData[0]);
    }

    [Fact]
    public void GetHashCode_ConsistentForSameData()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(data);

        // Act
        var hash1 = key.GetHashCode();
        var hash2 = key.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_SameDataDifferentInstances_ReturnsSameHash()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 1, 2, 3, 4 };
        var key1 = new CorrelationKey(data1);
        var key2 = new CorrelationKey(data2);

        // Act & Assert
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void Equals_SameData_ReturnsTrue()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 1, 2, 3, 4 };
        var key1 = new CorrelationKey(data1);
        var key2 = new CorrelationKey(data2);

        // Act & Assert
        Assert.True(key1.Equals(key2));
    }

    [Fact]
    public void Equals_DifferentData_ReturnsFalse()
    {
        // Arrange
        var key1 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        var key2 = new CorrelationKey(new byte[] { 5, 6, 7, 8 });

        // Act & Assert
        Assert.False(key1.Equals(key2));
    }

    [Fact]
    public void Equals_WithObject_SameData_ReturnsTrue()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 1, 2, 3, 4 };
        var key1 = new CorrelationKey(data1);
        object key2 = new CorrelationKey(data2);

        // Act & Assert
        Assert.True(key1.Equals(key2));
    }

    [Fact]
    public void Equals_WithNonCorrelationKeyObject_ReturnsFalse()
    {
        // Arrange
        var key = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        object other = new object();

        // Act & Assert
        Assert.False(key.Equals(other));
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var key = new CorrelationKey(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        Assert.False(key.Equals(null));
    }

    [Fact]
    public void OperatorEquals_SameData_ReturnsTrue()
    {
        // Arrange
        var key1 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        var key2 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        Assert.True(key1 == key2);
    }

    [Fact]
    public void OperatorEquals_DifferentData_ReturnsFalse()
    {
        // Arrange
        var key1 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        var key2 = new CorrelationKey(new byte[] { 5, 6, 7, 8 });

        // Act & Assert
        Assert.False(key1 == key2);
    }

    [Fact]
    public void OperatorNotEquals_SameData_ReturnsFalse()
    {
        // Arrange
        var key1 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        var key2 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        Assert.False(key1 != key2);
    }

    [Fact]
    public void OperatorNotEquals_DifferentData_ReturnsTrue()
    {
        // Arrange
        var key1 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        var key2 = new CorrelationKey(new byte[] { 5, 6, 7, 8 });

        // Act & Assert
        Assert.True(key1 != key2);
    }

    [Fact]
    public void TryCreate_ValidData_ReturnsKey()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4 };

        // Act
        var key = CorrelationKey.TryCreate(data);

        // Assert
        Assert.NotNull(key);
        Assert.Equal(data, key.Value.CorrelationData);
    }

    [Fact]
    public void TryCreate_NullData_ReturnsNull()
    {
        // Act
        var key = CorrelationKey.TryCreate(null);

        // Assert
        Assert.Null(key);
    }

    [Fact]
    public void TryCreate_EmptyData_ReturnsNull()
    {
        // Act
        var key = CorrelationKey.TryCreate(Array.Empty<byte>());

        // Assert
        Assert.Null(key);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var key = new CorrelationKey(new byte[] { 1, 2, 3, 4 });

        // Act
        var result = key.ToString();

        // Assert
        Assert.Contains("CorrelationKey", result);
    }

    [Fact]
    public void ToString_LongData_TruncatesDisplay()
    {
        // Arrange
        var longData = new byte[100];
        for (int i = 0; i < 100; i++)
            longData[i] = (byte)(i % 256);
        var key = new CorrelationKey(longData);

        // Act
        var result = key.ToString();

        // Assert
        Assert.Contains("...", result);
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        // Arrange
        var key1 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        var key2 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });
        var key3 = new CorrelationKey(new byte[] { 5, 6, 7, 8 });
        var dict = new Dictionary<CorrelationKey, string>();

        // Act
        dict[key1] = "value1";
        dict[key3] = "value3";

        // Assert
        Assert.Equal(2, dict.Count);
        Assert.Equal("value1", dict[key2]); // key2 equals key1
        Assert.Equal("value3", dict[key3]);
    }

    [Fact]
    public void DifferentDataLengths_NotEqual()
    {
        // Arrange
        var key1 = new CorrelationKey(new byte[] { 1, 2, 3 });
        var key2 = new CorrelationKey(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        Assert.False(key1.Equals(key2));
        Assert.NotEqual(key1.GetHashCode(), key2.GetHashCode());
    }
}
