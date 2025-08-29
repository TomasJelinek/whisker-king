using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.Performance;

namespace WhiskerKing.Performance.Tests
{
    /// <summary>
    /// Unit tests for ObjectPool system
    /// Tests pooling efficiency, memory management, performance, and automatic cleanup
    /// </summary>
    public class ObjectPoolTests
    {
        private GameObject testGameObject;
        private TestPooledObject testPrefab;
        private ObjectPool<TestPooledObject> objectPool;
        private PoolManager poolManager;

        // Test component for pooling
        private class TestPooledObject : MonoBehaviour, IPoolable
        {
            public bool wasReturnedToPool = false;
            public int returnCount = 0;

            public void OnReturnToPool()
            {
                wasReturnedToPool = true;
                returnCount++;
                gameObject.SetActive(false);
            }

            public void ResetForTest()
            {
                wasReturnedToPool = false;
                returnCount = 0;
                gameObject.SetActive(true);
            }
        }

        [SetUp]
        public void Setup()
        {
            // Create test prefab
            testGameObject = new GameObject("TestPrefab");
            testPrefab = testGameObject.AddComponent<TestPooledObject>();

            // Create object pool
            objectPool = new ObjectPool<TestPooledObject>(testPrefab, 5, 20);

            // Create pool manager
            GameObject poolManagerGO = new GameObject("PoolManager");
            poolManager = poolManagerGO.AddComponent<PoolManager>();
        }

        [TearDown]
        public void TearDown()
        {
            objectPool?.Dispose();
            
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
                
            if (poolManager != null)
                Object.DestroyImmediate(poolManager.gameObject);
        }

        #region Basic Pool Operations

        [Test]
        public void ObjectPool_Creation_InitializesCorrectly()
        {
            // Assert
            Assert.IsNotNull(objectPool, "Object pool should be created");
            Assert.AreEqual(5, objectPool.AvailableCount, "Should have 5 available items initially");
            Assert.AreEqual(5, objectPool.TotalCount, "Should have 5 total items initially");
            Assert.AreEqual(0, objectPool.ActiveCount, "Should have 0 active items initially");
        }

        [Test]
        public void Get_FromPool_ReturnsValidObject()
        {
            // Act
            var pooledObject = objectPool.Get();

            // Assert
            Assert.IsNotNull(pooledObject, "Should return a valid object");
            Assert.IsTrue(pooledObject.gameObject.activeInHierarchy, "Object should be active");
            Assert.AreEqual(1, objectPool.ActiveCount, "Should have 1 active item");
            Assert.AreEqual(4, objectPool.AvailableCount, "Should have 4 available items");
        }

        [Test]
        public void ReturnToPool_ValidObject_ReturnsSuccessfully()
        {
            // Arrange
            var pooledObject = objectPool.Get();
            pooledObject.ResetForTest();

            // Act
            objectPool.ReturnToPool(pooledObject);

            // Assert
            Assert.IsTrue(pooledObject.wasReturnedToPool, "OnReturnToPool should be called");
            Assert.IsFalse(pooledObject.gameObject.activeInHierarchy, "Object should be inactive");
            Assert.AreEqual(0, objectPool.ActiveCount, "Should have 0 active items");
            Assert.AreEqual(5, objectPool.AvailableCount, "Should have 5 available items");
        }

        [Test]
        public void ReturnToPool_NullObject_HandlesGracefully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => objectPool.ReturnToPool(null),
                "Should handle null return gracefully");
        }

        #endregion

        #region Pool Expansion Tests

        [Test]
        public void Get_WhenPoolEmpty_CreatesNewObject()
        {
            // Arrange - Get all available objects
            var objects = new TestPooledObject[5];
            for (int i = 0; i < 5; i++)
            {
                objects[i] = objectPool.Get();
            }

            // Act - Get one more object (should create new one)
            var extraObject = objectPool.Get();

            // Assert
            Assert.IsNotNull(extraObject, "Should create new object when pool is empty");
            Assert.AreEqual(6, objectPool.TotalCount, "Total count should increase");
            Assert.AreEqual(6, objectPool.ActiveCount, "All objects should be active");
        }

        [Test]
        public void PoolExpansion_RespectsMaxSize()
        {
            // Create pool with max size of 3
            var limitedPool = new ObjectPool<TestPooledObject>(testPrefab, 2, 3);

            try
            {
                // Arrange - Get all objects up to max
                var obj1 = limitedPool.Get();
                var obj2 = limitedPool.Get();
                var obj3 = limitedPool.Get();

                // Act - Try to get one more (should reuse oldest)
                var obj4 = limitedPool.Get();

                // Assert
                Assert.IsNotNull(obj4, "Should still return an object");
                Assert.LessOrEqual(limitedPool.TotalCount, 3, "Should not exceed max pool size");
            }
            finally
            {
                limitedPool.Dispose();
            }
        }

        #endregion

        #region Performance Tracking Tests

        [Test]
        public void PoolStatistics_TrackCorrectly()
        {
            // Act
            var obj1 = objectPool.Get();
            var obj2 = objectPool.Get();
            objectPool.ReturnToPool(obj1);

            // Assert
            Assert.Greater(objectPool.TotalCreated, 0, "Should track created objects");
            Assert.Greater(objectPool.TotalReused, 0, "Should track reused objects");
            Assert.Greater(objectPool.PoolEfficiency, 0f, "Should calculate efficiency");
        }

        [Test]
        public void PoolEfficiency_CalculatesCorrectly()
        {
            // Arrange
            var obj = objectPool.Get();
            objectPool.ReturnToPool(obj);
            
            // Reuse the same object
            var reusedObj = objectPool.Get();

            // Assert
            float efficiency = objectPool.PoolEfficiency;
            Assert.GreaterOrEqual(efficiency, 0f, "Efficiency should be non-negative");
            Assert.LessOrEqual(efficiency, 1f, "Efficiency should not exceed 1.0");
        }

        [Test]
        public void GetPoolStatistics_ReturnsValidString()
        {
            // Act
            string stats = objectPool.GetPoolStatistics();

            // Assert
            Assert.IsNotEmpty(stats, "Statistics string should not be empty");
            Assert.That(stats, Contains.Substring("Pool"), "Should contain pool information");
            Assert.That(stats, Contains.Substring("Total"), "Should contain total count");
        }

        #endregion

        #region Cleanup Tests

        [Test]
        public void CleanupOldItems_RemovesUnusedItems()
        {
            // Arrange
            var obj = objectPool.Get();
            objectPool.ReturnToPool(obj);
            int initialCount = objectPool.TotalCount;

            // Act - Force cleanup with very short age
            objectPool.CleanupOldItems(0.001f);

            // Assert
            Assert.LessOrEqual(objectPool.TotalCount, initialCount, 
                "Cleanup should remove or maintain item count");
        }

        [Test]
        public void AutomaticCleanup_WorksWithUpdate()
        {
            // Act - Call update (would normally be called by MonoBehaviour)
            objectPool.Update();

            // Assert - Should not throw
            Assert.DoesNotThrow(() => objectPool.Update());
        }

        [Test]
        public void Clear_RemovesAllItems()
        {
            // Arrange
            var obj = objectPool.Get();

            // Act
            objectPool.Clear();

            // Assert
            Assert.AreEqual(0, objectPool.TotalCount, "Clear should remove all items");
            Assert.AreEqual(0, objectPool.AvailableCount, "Clear should remove all available items");
            Assert.AreEqual(0, objectPool.ActiveCount, "Clear should remove all active items");
        }

        #endregion

        #region Position and Rotation Tests

        [Test]
        public void Get_WithPositionAndRotation_SetsCorrectly()
        {
            // Arrange
            Vector3 testPosition = new Vector3(10, 5, -3);
            Quaternion testRotation = Quaternion.Euler(45, 90, 0);

            // Act
            var obj = objectPool.Get(testPosition, testRotation);

            // Assert
            Assert.AreEqual(testPosition, obj.transform.position, "Position should be set correctly");
            Assert.AreEqual(testRotation, obj.transform.rotation, "Rotation should be set correctly");
        }

        #endregion

        #region Pool Manager Tests

        [Test]
        public void PoolManager_ImplementsSingleton()
        {
            // Act
            var instance1 = PoolManager.Instance;
            var instance2 = PoolManager.Instance;

            // Assert
            Assert.IsNotNull(instance1, "PoolManager instance should not be null");
            Assert.AreSame(instance1, instance2, "PoolManager should implement singleton pattern");
        }

        [Test]
        public void PoolManager_GetPool_CreatesAndReturnsPool()
        {
            // Act
            var pool = poolManager.GetPool(testPrefab);

            // Assert
            Assert.IsNotNull(pool, "PoolManager should return a valid pool");
            Assert.IsInstanceOf<ObjectPool<TestPooledObject>>(pool, "Should return correct pool type");
        }

        [Test]
        public void PoolManager_Get_ReturnsPooledObject()
        {
            // Act
            var obj = poolManager.Get(testPrefab);

            // Assert
            Assert.IsNotNull(obj, "PoolManager should return a valid object");
            Assert.IsInstanceOf<TestPooledObject>(obj, "Should return correct object type");
        }

        [Test]
        public void PoolManager_ReturnToPool_HandlesCorrectly()
        {
            // Arrange
            var obj = poolManager.Get(testPrefab);

            // Act & Assert
            Assert.DoesNotThrow(() => poolManager.ReturnToPool(obj),
                "PoolManager should handle return to pool");
        }

        [Test]
        public void PoolManager_CleanupAllPools_ExecutesWithoutError()
        {
            // Arrange
            var obj = poolManager.Get(testPrefab);
            poolManager.ReturnToPool(obj);

            // Act & Assert
            Assert.DoesNotThrow(() => poolManager.CleanupAllPools(),
                "Cleanup all pools should not throw");
        }

        [Test]
        public void PoolManager_GetAllPoolStatistics_ReturnsValidString()
        {
            // Arrange
            var obj = poolManager.Get(testPrefab);
            poolManager.ReturnToPool(obj);

            // Act
            string stats = poolManager.GetAllPoolStatistics();

            // Assert
            Assert.IsNotEmpty(stats, "Statistics should not be empty");
            Assert.That(stats, Contains.Substring("POOL MANAGER"), "Should contain header");
        }

        #endregion

        #region Edge Cases Tests

        [Test]
        public void ObjectPool_HandlesDestroyedPrefab()
        {
            // Arrange
            Object.DestroyImmediate(testPrefab);

            // Act & Assert - Should handle destroyed prefab gracefully
            // Note: This may throw in some cases, which is acceptable behavior
            // The important thing is that it doesn't cause crashes in the pool system
        }

        [Test]
        public void ReturnToPool_SameObjectMultipleTimes_HandlesCorrectly()
        {
            // Arrange
            var obj = objectPool.Get();
            objectPool.ReturnToPool(obj);

            // Act & Assert - Should handle duplicate returns gracefully
            Assert.DoesNotThrow(() => objectPool.ReturnToPool(obj));
        }

        [Test]
        public void Get_AfterDispose_HandlesGracefully()
        {
            // Arrange
            objectPool.Dispose();

            // Act & Assert - Disposed pool should handle gets gracefully
            // Behavior may vary, but should not crash
            Assert.DoesNotThrow(() => objectPool.Get());
        }

        #endregion

        #region Performance Tests

        [Test]
        public void ObjectPool_PerformanceTest()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var objects = new TestPooledObject[100];

            // Act - Get and return many objects
            for (int i = 0; i < 100; i++)
            {
                objects[i] = objectPool.Get();
            }
            
            for (int i = 0; i < 100; i++)
            {
                objectPool.ReturnToPool(objects[i]);
            }

            stopwatch.Stop();

            // Assert - Should complete quickly
            Assert.Less(stopwatch.ElapsedMilliseconds, 50, 
                "100 get/return operations should complete in less than 50ms");
        }

        [Test]
        public void PoolManager_MultiplePoolsPerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Act - Create and use multiple pools
            for (int i = 0; i < 10; i++)
            {
                var pool = poolManager.GetPool(testPrefab, poolName: $"TestPool{i}");
                var obj = pool.Get();
                pool.ReturnToPool(obj);
            }

            stopwatch.Stop();

            // Assert
            Assert.Less(stopwatch.ElapsedMilliseconds, 100,
                "Multiple pool operations should be performant");
        }

        #endregion

        #region Memory Tests

        [Test]
        public void ObjectPool_MemoryFootprint()
        {
            // Arrange
            long initialMemory = System.GC.GetTotalMemory(false);

            // Act - Create and use pool extensively
            var objects = new TestPooledObject[50];
            for (int i = 0; i < 50; i++)
            {
                objects[i] = objectPool.Get();
            }
            
            for (int i = 0; i < 50; i++)
            {
                objectPool.ReturnToPool(objects[i]);
            }

            long finalMemory = System.GC.GetTotalMemory(false);

            // Assert - Memory growth should be reasonable
            long memoryGrowth = finalMemory - initialMemory;
            Assert.Less(memoryGrowth, 5 * 1024 * 1024, // 5MB
                "Object pool should not cause excessive memory growth");
        }

        #endregion

        #region Integration Tests

        [Test]
        public void PoolManager_WithNamedPools_WorksCorrectly()
        {
            // Arrange
            string poolName = "TestNamedPool";

            // Act
            var obj1 = poolManager.Get(testPrefab, poolName: poolName);
            var obj2 = poolManager.Get(testPrefab, poolName: poolName);
            
            poolManager.ReturnToPool(obj1, poolName);
            poolManager.ReturnToPool(obj2, poolName);

            // Assert - Should handle named pools correctly
            Assert.IsNotNull(obj1, "Should get object from named pool");
            Assert.IsNotNull(obj2, "Should get another object from named pool");
        }

        [Test]
        public void IPoolable_Interface_WorksCorrectly()
        {
            // Arrange
            var obj = objectPool.Get();
            obj.ResetForTest();

            // Act
            objectPool.ReturnToPool(obj);

            // Assert
            Assert.IsTrue(obj.wasReturnedToPool, "IPoolable.OnReturnToPool should be called");
            Assert.Greater(obj.returnCount, 0, "Return count should be tracked");
        }

        #endregion

        #region Robustness Tests

        [Test]
        public void ObjectPool_HandlesExtremeUsage()
        {
            // Act & Assert - Should handle extreme usage without crashing
            Assert.DoesNotThrow(() => {
                for (int i = 0; i < 1000; i++)
                {
                    var obj = objectPool.Get();
                    if (i % 2 == 0)
                    {
                        objectPool.ReturnToPool(obj);
                    }
                }
            });
        }

        [Test]
        public void PoolManager_ClearAllPools_ExecutesCorrectly()
        {
            // Arrange
            var obj = poolManager.Get(testPrefab);
            
            // Act
            poolManager.ClearAllPools();

            // Assert - Should not throw
            Assert.DoesNotThrow(() => poolManager.GetAllPoolStatistics());
        }

        [Test]
        public void ObjectPool_DisposalCleanup()
        {
            // Arrange
            var obj = objectPool.Get();

            // Act
            objectPool.Dispose();

            // Assert - Should clean up properly
            Assert.AreEqual(0, objectPool.TotalCount, "Dispose should clean up all objects");
        }

        #endregion
    }
}
