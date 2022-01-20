using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.EditorTests
{
    public class ClusterCameraTest
    {
        List<Camera> m_Cameras = new();

        [OneTimeSetUp]
        public void LoadEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [SetUp]
        public void SetUp()
        {
            m_Cameras.Add(new GameObject("camera1", typeof(ClusterCamera)).GetComponent<Camera>());
            m_Cameras.Add(new GameObject("camera2", typeof(ClusterCamera)).GetComponent<Camera>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var camera in m_Cameras)
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void TracksActiveCamera()
        {
            m_Cameras[0].gameObject.SetActive(false);
            Assert.That(ClusterCameraManager.Instance.ActiveCamera, Is.EqualTo(m_Cameras[1]));
            m_Cameras[1].gameObject.SetActive(false);
            Assert.That(ClusterCameraManager.Instance.ActiveCamera, Is.Null);
            m_Cameras[0].gameObject.SetActive(true);
            Assert.That(ClusterCameraManager.Instance.ActiveCamera, Is.EqualTo(m_Cameras[0]));
        }
    }
}
