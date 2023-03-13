using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.LiveEditing.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine.SceneManagement;

namespace Unity.LiveEditing.Tests.Editor
{
    class SceneChangeTrackerTests
    {
        static readonly string k_SceneDirectory = "Assets/Tests/Scenes";
        static readonly string k_Scene1Path = $"{k_SceneDirectory}/Scene1.unity";
        static readonly string k_Scene2Path = $"{k_SceneDirectory}/Scene2.unity";
        
        SceneChangeTracker m_SceneChangeTracker;
        
        readonly Queue<(int, GameObject)> m_GameObjectAddedChanges = new();
        readonly Queue<int> m_GameObjectDestroyedChanges = new();
        readonly Queue<(int, GameObject, Scene, GameObject)> m_GameObjectParentChanges = new();
        readonly Queue<(int, GameObject, int)> m_GameObjectIndexChanges = new();
        readonly Queue<(int, GameObject, SerializedProperty)> m_GameObjectPropertyChanges = new();
        readonly Queue<(int, Component)> m_ComponentAddedChanges = new();
        readonly Queue<int> m_ComponentDestroyedChanges = new();
        readonly Queue<(int, Component, int)> m_ComponentIndexChanges = new();
        readonly Queue<(int, Component, SerializedProperty)> m_ComponentPropertyChanges = new();

        int GetChangeCount()
        {
            return m_GameObjectAddedChanges.Count +
                m_GameObjectDestroyedChanges.Count +
                m_GameObjectParentChanges.Count +
                m_GameObjectIndexChanges.Count +
                m_GameObjectPropertyChanges.Count +
                m_ComponentAddedChanges.Count +
                m_ComponentDestroyedChanges.Count +
                m_ComponentIndexChanges.Count +
                m_ComponentPropertyChanges.Count;
        }

        void ClearChanges()
        {
            m_GameObjectAddedChanges.Clear();
            m_GameObjectDestroyedChanges.Clear();
            m_GameObjectParentChanges.Clear();
            m_GameObjectIndexChanges.Clear();
            m_GameObjectPropertyChanges.Clear();
            m_ComponentAddedChanges.Clear();
            m_ComponentDestroyedChanges.Clear();
            m_ComponentIndexChanges.Clear();
            m_ComponentPropertyChanges.Clear();
        }
        
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Undo.ClearAll();
        }
        
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            
            EditorSceneManager.OpenScene(k_Scene1Path, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(k_Scene2Path, OpenSceneMode.Additive);

            ClearChanges();
            
            m_SceneChangeTracker = new SceneChangeTracker
            {
                MaxUpdateTimeSlice = TimeSpan.FromMilliseconds(1000),
                EnablePolling = true,
                PollingPeriod = TimeSpan.FromMilliseconds(1000),
            };

            m_SceneChangeTracker.GameObjectAdded += (id, gameObject) => m_GameObjectAddedChanges.Enqueue((id, gameObject));
            m_SceneChangeTracker.GameObjectDestroyed += (id) => m_GameObjectDestroyedChanges.Enqueue(id);
            m_SceneChangeTracker.GameObjectParentChanged += (id, gameObject, scene, parent) => m_GameObjectParentChanges.Enqueue((id, gameObject, scene, parent));
            m_SceneChangeTracker.GameObjectIndexChanged += (id, gameObject, index) => m_GameObjectIndexChanges.Enqueue((id, gameObject, index));
            m_SceneChangeTracker.GameObjectPropertyChanged += (id, gameObject, property) => m_GameObjectPropertyChanges.Enqueue((id, gameObject, property));
            m_SceneChangeTracker.ComponentAdded += (id, component) => m_ComponentAddedChanges.Enqueue((id, component));
            m_SceneChangeTracker.ComponentDestroyed += (id) => m_ComponentDestroyedChanges.Enqueue(id);
            m_SceneChangeTracker.ComponentIndexChanged += (id, component, index) => m_ComponentIndexChanges.Enqueue((id, component, index));
            m_SceneChangeTracker.ComponentPropertyChanged += (id, component, property) => m_ComponentPropertyChanges.Enqueue((id, component, property));

            m_SceneChangeTracker.Start();
        }

        [TearDown]
        public void TearDown()
        {
            m_SceneChangeTracker.Stop();
            
            if (m_SceneChangeTracker != null)
            {
                m_SceneChangeTracker.Dispose();
                m_SceneChangeTracker = null;
            }
        }

        [UnityTest]
        public IEnumerator TestNoChangesDetected()
        {
            yield return null;
            
            m_SceneChangeTracker.Update();

            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(0, GetChangeCount());
        }
        
        internal class GameObjectConfig
        {
            public string name { get; set; }
            public string scenePath { get; set; }
            public string parentPath { get; set; }
            public int? siblingIndex { get; set; }

            /// <inheritdoc />
            public override string ToString()
            {
                return $"name:\"{name}\" scenePath:\"{scenePath}\" parentPath:\"{parentPath}\" index:{siblingIndex}";
            }
        }

        static IEnumerable TestAddGameObjects()
        {
            yield return new TestCaseData(new GameObjectConfig
            {
                name = "go1",
                scenePath = k_Scene1Path,
            }).Returns(null);
            yield return new TestCaseData(new GameObjectConfig
            {
                name = "go2",
                scenePath = k_Scene2Path,
                siblingIndex = 1,
            }).Returns(null);
            yield return new TestCaseData(new GameObjectConfig
            {
                name = "go3",
                scenePath = k_Scene1Path,
                parentPath = "Parent1",
                siblingIndex = 0,
            }).Returns(null);
            yield return new TestCaseData(new GameObjectConfig
            {
                name = "go4",
                scenePath = k_Scene2Path,
                parentPath = "Parent2",
                siblingIndex = 1,
            }).Returns(null);
            yield return new TestCaseData(new GameObjectConfig
            {
                name = "go5",
                scenePath = k_Scene1Path,
                parentPath = "Parent1/Child1",
            }).Returns(null);
            yield return new TestCaseData(new GameObjectConfig
            {
                name = "go6",
                scenePath = k_Scene2Path,
                parentPath = "Parent2/Child2",
            }).Returns(null);
        }

        static GameObject CreateGameObjects(GameObjectConfig config)
        {
            var go = new GameObject(config.name);

            if (!string.IsNullOrEmpty(config.scenePath))
            {
                SceneManager.MoveGameObjectToScene(go, SceneManager.GetSceneByPath(config.scenePath));
            }
            if (!string.IsNullOrEmpty(config.parentPath))
            {
                go.transform.SetParent(GameObject.Find(config.parentPath).transform, false);
            }
            if (config.siblingIndex != null)
            {
                go.transform.SetSiblingIndex(config.siblingIndex.Value);
            }

            return go;
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddGameObjects))]
        public IEnumerator TestAddGameObject(GameObjectConfig config)
        {
            var go = CreateGameObjects(config);
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, m_GameObjectAddedChanges.Count);

            var change = m_GameObjectAddedChanges.Dequeue();
            
            Assert.AreEqual(go.GetInstanceID(), change.Item1);
            Assert.AreEqual(go, change.Item2);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddGameObjects))]
        public IEnumerator TestAddGameObjectInitializesParent(GameObjectConfig config)
        {
            var go = CreateGameObjects(config);
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, m_GameObjectParentChanges.Count);

            var change = m_GameObjectParentChanges.Dequeue();
            
            Assert.AreEqual(go.GetInstanceID(), change.Item1);
            Assert.AreEqual(go, change.Item2);
            Assert.AreEqual(go.scene, change.Item3);
            Assert.AreEqual(go.transform.parent != null ? go.transform.parent.gameObject : null, change.Item4);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddGameObjects))]
        public IEnumerator TestAddGameObjectInitializesIndex(GameObjectConfig config)
        {
            var go = CreateGameObjects(config);
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            var indexChanges = m_GameObjectIndexChanges.Where(x => x.Item2 == go);

            Assert.AreEqual(1, indexChanges.Count());

            if (!indexChanges.Any())
            {
                yield break;
            }
            
            var change = indexChanges.First();
            
            Assert.AreEqual(go.GetInstanceID(), change.Item1);
            Assert.AreEqual(go, change.Item2);
            Assert.AreEqual(go.transform.GetSiblingIndex(), change.Item3);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddGameObjects))]
        public IEnumerator TestAddGameObjectInitializesProperties(GameObjectConfig config)
        {
            var go = CreateGameObjects(config);
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            var namePropertyChanges = m_GameObjectPropertyChanges.Where(x => x.Item3.name == "m_Name");

            Assert.AreEqual(1, namePropertyChanges.Count());

            if (!namePropertyChanges.Any())
            {
                yield break;
            }
            
            var change = namePropertyChanges.First();
            
            Assert.AreEqual(go.GetInstanceID(), change.Item1);
            Assert.AreEqual(go, change.Item2);
            Assert.AreEqual(go.name, change.Item3.stringValue);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddGameObjects))]
        public IEnumerator TestAddGameObjectIgnoredIfDestroyed(GameObjectConfig config)
        {
            var go = CreateGameObjects(config);
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            Undo.DestroyObjectImmediate(go);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(0, m_GameObjectAddedChanges.Count);
        }
        
        static IEnumerable TestDestroyGameObjects()
        {
            yield return new TestCaseData("Parent1").Returns(null);
            yield return new TestCaseData("Parent2").Returns(null);
            yield return new TestCaseData("Parent1/Child1").Returns(null);
            yield return new TestCaseData("Parent2/Child2").Returns(null);
        }
        
        [UnityTest, TestCaseSource(nameof(TestDestroyGameObjects))]
        public IEnumerator TestDestroyGameObject(string goPath)
        {
            var go = GameObject.Find(goPath);
            var id = go.GetInstanceID();
            Undo.DestroyObjectImmediate(go);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, m_GameObjectDestroyedChanges.Count);

            var change = m_GameObjectDestroyedChanges.Dequeue();

            Assert.AreEqual(id, change);
        }
        
        [UnityTest]
        public IEnumerator TestDestroyGameObjectIgnoredIfNotAdded()
        {
            var go = new GameObject();
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            Undo.DestroyObjectImmediate(go);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(0, m_GameObjectDestroyedChanges.Count);
        }

        static IEnumerable TestGameObjectsParents()
        {
            yield return new TestCaseData("Parent1/Child1", k_Scene1Path, "").Returns(null);
            yield return new TestCaseData("Parent1/Child1", k_Scene2Path, "").Returns(null);
            yield return new TestCaseData("Parent1/Child2", k_Scene1Path, "Parent1/Child1").Returns(null);
            yield return new TestCaseData("Parent1", k_Scene2Path, "Parent2").Returns(null);
        }

        [UnityTest, TestCaseSource(nameof(TestGameObjectsParents))]
        public IEnumerator TestChangeGameObjectParent(string goPath, string newScenePath, string newParentPath)
        {
            var go = GameObject.Find(goPath);
            var newScene = SceneManager.GetSceneByPath(newScenePath);
            var newParent = GameObject.Find(newParentPath);

            // moving a game object to a new scene requires it is a root object
            Undo.SetTransformParent(go.transform, null, "");
            Undo.MoveGameObjectToScene(go, newScene, "");
            Undo.SetTransformParent(go.transform, newParent != null ? newParent.transform : null, "");
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, m_GameObjectParentChanges.Count);

            var change = m_GameObjectParentChanges.Dequeue();

            Assert.AreEqual(go.GetInstanceID(), change.Item1);
            Assert.AreEqual(go, change.Item2);
            Assert.AreEqual(newScene, change.Item3);
            Assert.AreEqual(newParent, change.Item4);
        }

        static IEnumerable TestGameObjectsIndex()
        {
            yield return new TestCaseData("Main Camera", 0).Returns(null);
            yield return new TestCaseData("Directional Light", 2).Returns(null);
            yield return new TestCaseData("Parent1", 2).Returns(null);
            yield return new TestCaseData("Parent2", 0).Returns(null);
            yield return new TestCaseData("Parent1/Child2", 0).Returns(null);
            yield return new TestCaseData("Parent2/Child1", 1).Returns(null);
        }

        [UnityTest, TestCaseSource(nameof(TestGameObjectsIndex))]
        public IEnumerator TestChangeGameObjectIndex(string goPath, int newIndex)
        {
            var go = GameObject.Find(goPath);
            var transform = go.transform;
            var parent = transform.parent;
            var oldIndex = transform.GetSiblingIndex();
            
            go.transform.SetSiblingIndex(newIndex);
            
            EditorSceneManager.MarkAllScenesDirty();
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            var diff = Mathf.Abs(oldIndex - newIndex);
            var expectedIndexChanges = diff == 0 ? 0 : diff + 1;
            
            Assert.AreEqual(expectedIndexChanges, m_GameObjectIndexChanges.Count);

            if (expectedIndexChanges == 0)
            {
                yield break;
            }
            
            var siblings = (
                parent == null ?
                go.scene.GetRootGameObjects().Select(g => g.transform) :
                Enumerable.Range(0, parent.childCount).Select(i => parent.GetChild(i))
            ).ToArray();

            for (var i = Mathf.Min(oldIndex, newIndex); i <= Mathf.Max(oldIndex, newIndex); i++)
            {
                var change = m_GameObjectIndexChanges.Dequeue();
                var sibling = siblings[i].gameObject;
                
                Assert.AreEqual(sibling.GetInstanceID(), change.Item1);
                Assert.AreEqual(sibling, change.Item2);
                Assert.AreEqual(sibling.transform.GetSiblingIndex(), change.Item3);
            }
        }

        [UnityTest]
        public IEnumerator TestChangeGameObjectPropertySingle()
        {
            var go = GameObject.Find("Main Camera");
            go.name = "new name";
            
            EditorSceneManager.MarkAllScenesDirty();
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, GetChangeCount());
            Assert.AreEqual(1, m_GameObjectPropertyChanges.Count);

            var change = m_GameObjectPropertyChanges.Dequeue();
            
            Assert.AreEqual(go.GetInstanceID(), change.Item1);
            Assert.AreEqual(go, change.Item2);
            Assert.AreEqual(go.name, change.Item3.stringValue);
        }

        [UnityTest]
        public IEnumerator TestChangeGameObjectPropertyMultiple()
        {
            var go = GameObject.Find("Parent2/Child1");
            go.SetActive(false);
            go.layer = 2;
            
            EditorSceneManager.MarkAllScenesDirty();
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(2, GetChangeCount());
            Assert.AreEqual(2, m_GameObjectPropertyChanges.Count);

            while (m_GameObjectPropertyChanges.TryDequeue(out var change))
            {
                Assert.AreEqual(go.GetInstanceID(), change.Item1);
                Assert.AreEqual(go, change.Item2);

                switch (change.Item3.name)
                {
                    case "m_Layer":
                        Assert.AreEqual(go.layer, change.Item3.intValue);
                        break;
                    case "m_IsActive":
                        Assert.AreEqual(go.activeSelf, change.Item3.boolValue);
                        break;
                }
            }
        }

        static IEnumerable TestAddComponents()
        {
            yield return new TestCaseData("Parent1").Returns(null);
            yield return new TestCaseData("Parent1/Child1").Returns(null);
            yield return new TestCaseData("Parent2").Returns(null);
            yield return new TestCaseData("Parent2/Child2").Returns(null);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddComponents))]
        public IEnumerator TestAddComponentSingle(string goPath)
        {
            var go = GameObject.Find(goPath);
            var comp1 = Undo.AddComponent<BoxCollider>(go);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, m_ComponentAddedChanges.Count);

            var change = m_ComponentAddedChanges.Dequeue();
            
            Assert.AreEqual(comp1.GetInstanceID(), change.Item1);
            Assert.AreEqual(comp1, change.Item2);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddComponents))]
        public IEnumerator TestAddComponentMultiple(string goPath)
        {
            var go = GameObject.Find(goPath);
            var comp1 = Undo.AddComponent<BoxCollider>(go);
            var comp2 = Undo.AddComponent<Rigidbody>(go);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(2, m_ComponentAddedChanges.Count);

            var change1 = m_ComponentAddedChanges.Dequeue();
            
            Assert.AreEqual(comp1.GetInstanceID(), change1.Item1);
            Assert.AreEqual(comp1, change1.Item2);
            
            var change2 = m_ComponentAddedChanges.Dequeue();
            
            Assert.AreEqual(comp2.GetInstanceID(), change2.Item1);
            Assert.AreEqual(comp2, change2.Item2);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddComponents))]
        public IEnumerator TestAddComponentInitializesIndex(string goPath)
        {
            var go = GameObject.Find(goPath);
            var comp1 = Undo.AddComponent<BoxCollider>(go);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            var indexChanges = m_ComponentIndexChanges.Where(x => x.Item2 == comp1);

            Assert.AreEqual(1, indexChanges.Count());

            var change = indexChanges.First();
            
            Assert.AreEqual(comp1.GetInstanceID(), change.Item1);
            Assert.AreEqual(comp1, change.Item2);
            Assert.AreEqual(Array.IndexOf(go.GetComponents<Component>(), comp1), change.Item3);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddComponents))]
        public IEnumerator TestAddComponentInitializesProperties(string goPath)
        {
            var go = GameObject.Find(goPath);
            var comp1 = Undo.AddComponent<BoxCollider>(go);
            comp1.enabled = false;
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            var enabledPropertyChanges = m_ComponentPropertyChanges.Where(x => x.Item3.name == "m_Enabled");

            Assert.AreEqual(1, enabledPropertyChanges.Count());

            var change = enabledPropertyChanges.First();
            
            Assert.AreEqual(comp1.GetInstanceID(), change.Item1);
            Assert.AreEqual(comp1, change.Item2);
            Assert.AreEqual(comp1.enabled, change.Item3.boolValue);
        }
        
        [UnityTest, TestCaseSource(nameof(TestAddComponents))]
        public IEnumerator TestAddComponentIgnoredIfDestroyed(string goPath)
        {
            var go = GameObject.Find(goPath);
            var comp1 = Undo.AddComponent<BoxCollider>(go);
            Undo.DestroyObjectImmediate(comp1);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(0, m_ComponentAddedChanges.Count);
        }
        
        static IEnumerable TestDestroyComponents()
        {
            yield return new TestCaseData("Main Camera", typeof(Camera)).Returns(null);
            yield return new TestCaseData("CubeWithChildSphere", typeof(MeshRenderer)).Returns(null);
            yield return new TestCaseData("Parent1/Child1", typeof(BoxCollider)).Returns(null);
            yield return new TestCaseData("Parent2/Child2", typeof(BoxCollider)).Returns(null);
        }
        
        [UnityTest, TestCaseSource(nameof(TestDestroyComponents))]
        public IEnumerator TestDestroyComponent(string goPath, Type compType)
        {
            var go = GameObject.Find(goPath);
            var comp1 = go.GetComponent(compType);
            var id = comp1.GetInstanceID();
            Undo.DestroyObjectImmediate(comp1);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, m_ComponentDestroyedChanges.Count);

            var change = m_ComponentDestroyedChanges.Dequeue();

            Assert.AreEqual(id, change);
        }
        
        [UnityTest]
        public IEnumerator TestDestroyComponentIgnoredIfNotAdded()
        {
            var go = new GameObject();
            Undo.AddComponent<BoxCollider>(go);
            Undo.DestroyObjectImmediate(go);
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(0, m_ComponentDestroyedChanges.Count);
        }

        static IEnumerable TestComponentsIndex()
        {
            yield return new TestCaseData("Main Camera", typeof(Camera), 1).Returns(null);
            yield return new TestCaseData("Main Camera", typeof(Camera), 2).Returns(null);
            yield return new TestCaseData("Script1", typeof(BoxCollider), 1).Returns(null);
            yield return new TestCaseData("Script1", typeof(BoxCollider), 4).Returns(null);
        }

        [UnityTest, TestCaseSource(nameof(TestComponentsIndex))]
        public IEnumerator TestChangeComponentIndex(string goPath, Type compType, int newIndex)
        {
            var go = GameObject.Find(goPath);
            var components = go.GetComponents<Component>();
            var component = go.GetComponent(compType);
            var oldIndex = Array.IndexOf(components, component);

            for (var i = oldIndex; i < newIndex; i++)
            {
                ComponentUtility.MoveComponentDown(component);
            }
            for (var i = newIndex; i < oldIndex; i++)
            {
                ComponentUtility.MoveComponentUp(component);
            }

            EditorSceneManager.MarkAllScenesDirty();
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            var diff = Mathf.Abs(oldIndex - newIndex);
            var expectedIndexChanges = diff == 0 ? 0 : diff + 1;
            
            Assert.AreEqual(expectedIndexChanges, m_ComponentIndexChanges.Count);

            if (expectedIndexChanges == 0)
            {
                yield break;
            }
            
            components = go.GetComponents<Component>();
            
            for (var i = Mathf.Min(oldIndex, newIndex); i <= Mathf.Max(oldIndex, newIndex); i++)
            {
                var change = m_ComponentIndexChanges.Dequeue();
                var comp = components[i];
                
                Assert.AreEqual(comp.GetInstanceID(), change.Item1);
                Assert.AreEqual(comp, change.Item2);
                Assert.AreEqual(Array.IndexOf(components, comp), change.Item3);
            }
        }

        [UnityTest]
        public IEnumerator TestChangeComponentPropertySingle()
        {
            var go = GameObject.Find("Script1");
            var comp = go.GetComponent<BoxCollider>();
            comp.size = new Vector3(5f, 1f, 1f);
            
            EditorSceneManager.MarkAllScenesDirty();
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(1, GetChangeCount());
            Assert.AreEqual(1, m_ComponentPropertyChanges.Count);

            var change = m_ComponentPropertyChanges.Dequeue();
            
            Assert.AreEqual(comp.GetInstanceID(), change.Item1);
            Assert.AreEqual(comp, change.Item2);
            Assert.AreEqual(comp.size, change.Item3.vector3Value);
        }

        [UnityTest]
        public IEnumerator TestChangeComponentPropertyMultiple()
        {
            var go = GameObject.Find("Script1");
            var comp = go.GetComponent<BoxCollider>();
            comp.enabled = false;
            comp.center = new Vector3(2f, 2f, 1f);
            
            EditorSceneManager.MarkAllScenesDirty();
            
            yield return null;
            
            m_SceneChangeTracker.Update();

            Assert.AreEqual(2, GetChangeCount());
            Assert.AreEqual(2, m_ComponentPropertyChanges.Count);

            while (m_ComponentPropertyChanges.TryDequeue(out var change))
            {
                Assert.AreEqual(comp.GetInstanceID(), change.Item1);
                Assert.AreEqual(comp, change.Item2);

                switch (change.Item3.name)
                {
                    case "m_Enabled":
                        Assert.AreEqual(comp.enabled, change.Item3.boolValue);
                        break;
                    case "m_Center":
                        Assert.AreEqual(comp.center, change.Item3.vector3Value);
                        break;
                }
            }
        }

        [UnityTest]
        public IEnumerator TestIgnoresChangesOnceStopped()
        {
            m_SceneChangeTracker.Stop();
            
            var go = new GameObject();
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            
            yield return null;
            
            m_SceneChangeTracker.Update();
            
            Assert.AreEqual(0, GetChangeCount());
        }
        
        [UnityTest]
        public IEnumerator TestDetectsChangesAfterRestart()
        {
            m_SceneChangeTracker.Stop();
            m_SceneChangeTracker.Start();

            var go = new GameObject();
            Undo.RegisterCreatedObjectUndo(go, $"Add {go.name}");
            
            yield return null;
            
            m_SceneChangeTracker.Update();
            
            Assert.AreNotEqual(0, GetChangeCount());
        }
    }
}
