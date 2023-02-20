using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.LiveEditing.Editor;
using UnityEditor.SceneManagement;

namespace Unity.LiveEditing.Tests.Editor
{
    class SceneChangeTrackerTests
    {
        SceneSetup[] m_SceneSetups;
        SceneChangeTracker m_SceneChangeTracker;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_SceneSetups = EditorSceneManager.GetSceneManagerSetup();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorSceneManager.RestoreSceneManagerSetup(m_SceneSetups);
        }
        
        [SetUp]
        public void SetUp()
        {
            m_SceneChangeTracker = new SceneChangeTracker();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_SceneChangeTracker != null)
            {
                m_SceneChangeTracker.Dispose();
                m_SceneChangeTracker = null;
            }
        }
        
        [UnityTest]
        public IEnumerator TestClientSendAndReceive()
        {
            yield break;
        }
    }
}
