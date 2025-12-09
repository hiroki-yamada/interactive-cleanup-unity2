using SIGVerse.Common;
using SIGVerse.Human.IK;
using SIGVerse.Human.VR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Management;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public interface IAvatarMotionHandler : IEventSystemHandler
	{
		void OnAvatarPointByLeft();
		void OnAvatarPointByRight();
		void OnAvatarPressA();
		void OnAvatarPressX();
	}

	public class CleanupAvatarVRController : MonoBehaviour
	{
		//public GameObject avatar;
		public GameObject xrOrigin;

		public Laser laserLeft;
		public Laser laserRight;

		//public GameObject cameraRig;
		//public GameObject eyeAnchor;

		public Animator   avatarAnimator;

		public List<GameObject> avatarMotionDestinations;

		public GameObject initialPositionMarker;

		public CapsuleCollider rootCapsuleCollider;

		public GameObject rosBridgeScripts;

		//----------------------------------------

		private List<CapsuleCollider> capsuleColliders;

		private SimpleHumanVRController simpleHumanVRController;
		private SimpleIK simpleIK;
		private List<HumanHandController> vrHandControllers;

		private XRLoader activeLoader;
		private InputDevice leftHandDevice;
		private InputDevice rightHandDevice;

		void Awake()
		{
			this.capsuleColliders      = this.GetComponentsInChildren<CapsuleCollider>().ToList();
			this.capsuleColliders.Remove(this.rootCapsuleCollider);

			this.simpleHumanVRController = this.GetComponentInChildren<SimpleHumanVRController>();
			this.simpleIK                = this.GetComponentInChildren<SimpleIK>();
			this.vrHandControllers       = this.GetComponentsInChildren<HumanHandController>().ToList();

			ExecutionMode executionMode = (ExecutionMode)Enum.ToObject(typeof(ExecutionMode), CleanupConfig.Instance.configFileInfo.executionMode);

			switch (executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					//this.cameraRig.SetActive(false);
					this.xrOrigin.SetActive(false);
					this.avatarAnimator.enabled = false;

					this.rootCapsuleCollider.enabled = false;
					foreach(CapsuleCollider capsuleCollider in this.capsuleColliders){ capsuleCollider.enabled = true; }

					this.simpleHumanVRController.enabled = false;
					this.simpleIK               .enabled = false;
					foreach(HumanHandController vrHandController in this.vrHandControllers){ vrHandController.enabled = false; }

					this.initialPositionMarker.SetActive(false);

					Rigidbody[] rigidbodies = this.GetComponentsInChildren<Rigidbody>(true);
					foreach(Rigidbody rigidbody in rigidbodies){ rigidbody.useGravity = false; }

					this.enabled = false;

					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					StartCoroutine(this.InitializeHumanForDataGeneration());

					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}

		private IEnumerator InitializeHumanForDataGeneration()
		{
			yield return this.InitializeXRForDataGeneration();

			this.EnableScriptsForDataGeneration();
		}

		private IEnumerator InitializeXRForDataGeneration()
		{
			// Initialize XR System
			XRManagerSettings xrManagerSettings = XRGeneralSettings.Instance.Manager;

			if (xrManagerSettings == null) { SIGVerseLogger.Error("xrManagerSettings == null"); yield break; }

			if (xrManagerSettings.activeLoader == null)
			{
				yield return xrManagerSettings.InitializeLoader();
			}

			this.activeLoader = xrManagerSettings.activeLoader;

			if (this.activeLoader == null)
			{
				Debug.LogError("Initializing XR Failed.");
				yield break;
			}

			xrManagerSettings.activeLoader.Start();

			//SteamVR_Actions.sigverse.Activate(SteamVR_Input_Sources.Any);
		}

		private void EnableScriptsForDataGeneration()
		{
			this.xrOrigin.SetActive(true);
			this.avatarAnimator.enabled = true;

			this.rootCapsuleCollider.enabled = true;
			foreach(CapsuleCollider capsuleCollider in this.capsuleColliders){ capsuleCollider.enabled = false; }

			this.simpleHumanVRController.enabled = true;
			this.simpleIK               .enabled = true;
			foreach(HumanHandController vrHandController in this.vrHandControllers){ vrHandController.enabled = true; }

			this.initialPositionMarker.SetActive(true);

			// Enable VR Scripts
			this.xrOrigin.GetComponentInChildren<XROrigin>().enabled = true;
			this.xrOrigin.GetComponentInChildren<InputActionManager>().enabled = true;

			this.xrOrigin.GetComponentInChildren<Camera>().enabled = true;
//			this.xrOrigin.GetComponentInChildren<AudioListener>().enabled = true;
			Array.ForEach(this.xrOrigin.GetComponentsInChildren<TrackedPoseDriver>(), x => x.enabled = true);
			this.xrOrigin.GetComponentInChildren<SIGVerse.Human.IK.AnchorPostureCalculator>().enabled = true;


			StartCoroutine(SIGVerseUtils.GetXrDevice(XRNode.LeftHand,  x => this.leftHandDevice  = x));
			StartCoroutine(SIGVerseUtils.GetXrDevice(XRNode.RightHand, x => this.rightHandDevice = x));

			//this.GetComponent<Player>().enabled = true;

			//this.cameraRig.GetComponent<SIGVerse.Human.IK.AnchorPostureCalculator>().enabled = true;

			//SteamVR_Behaviour_Pose[] steamVrBehaviourPoses = this.cameraRig.GetComponentsInChildren<SteamVR_Behaviour_Pose>();
			//foreach(SteamVR_Behaviour_Pose steamVrBehaviourPose in steamVrBehaviourPoses){ steamVrBehaviourPose.enabled = true;}

			//Hand[] hands = this.cameraRig.GetComponentsInChildren<Hand>(true);
			//foreach(Hand hand in hands){ hand.enabled = true; }

			//this.eyeAnchor.GetComponent<Camera>().enabled = true;
			//this.eyeAnchor.GetComponent<SteamVR_CameraHelper>().enabled = true;
		}

		void Update()
		{
			// Enable/Disable Laser of Left hand
//			if (SteamVR_Actions.sigverse_SqueezeMiddle.GetAxis(SteamVR_Input_Sources.LeftHand) > 0.95)
			if(this.leftHandDevice.TryGetFeatureValue(CommonUsages.grip, out float leftHandTriggerValue) && leftHandTriggerValue > 0.95)
			{
				if(!this.laserLeft.gameObject.activeInHierarchy)
				{
					this.laserLeft.Activate(); 
				}
			}
			else
			{
				if(this.laserLeft.gameObject.activeInHierarchy)
				{
					this.laserLeft.Deactivate();
				}
			}

			// Enable/Disable Laser of Right hand
//				if (SteamVR_Actions.sigverse_SqueezeMiddle.GetAxis(SteamVR_Input_Sources.RightHand) > 0.95)
			if (this.rightHandDevice.TryGetFeatureValue(CommonUsages.grip, out float rightHandTriggerValue) && rightHandTriggerValue > 0.95)
			{
				if(!this.laserRight.gameObject.activeInHierarchy)
				{
					this.laserRight.Activate();
				}
			}
			else
			{
				if(this.laserRight.gameObject.activeInHierarchy)
				{
					this.laserRight.Deactivate();
				}
			}


			if (this.laserLeft.gameObject.activeInHierarchy)
			{
//				if (SteamVR_Actions.sigverse_PressIndex.GetStateDown(SteamVR_Input_Sources.LeftHand))
				if(this.leftHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool leftTriggerButton) && leftTriggerButton) // Index finger
				{
					string selectedTargetName = this.laserLeft.Point(true);

					foreach (GameObject avatarMotionDestination in this.avatarMotionDestinations)
					{
						ExecuteEvents.Execute<IAvatarMotionHandler>
						(
							target: avatarMotionDestination,
							eventData: null,
							functor: (reciever, eventData) => reciever.OnAvatarPointByLeft()
						);
					}

					Debug.Log("selectedTargetName=" + selectedTargetName);
				}
			}

			if (this.laserRight.gameObject.activeInHierarchy)
			{
//				if (SteamVR_Actions.sigverse_PressIndex.GetStateDown(SteamVR_Input_Sources.RightHand))
				if(this.rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool rightTriggerButton) && rightTriggerButton) // Index finger
				{
					string selectedTargetName = this.laserRight.Point(true);

					foreach (GameObject avatarMotionDestination in this.avatarMotionDestinations)
					{
						ExecuteEvents.Execute<IAvatarMotionHandler>
						(
							target: avatarMotionDestination,
							eventData: null,
							functor: (reciever, eventData) => reciever.OnAvatarPointByRight()
						);
					}

					Debug.Log("selectedTargetName=" + selectedTargetName);
				}
			}

//			if(SteamVR_Actions.sigverse_PressNearButton.GetStateDown(SteamVR_Input_Sources.RightHand))
			if(this.rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool rightPrimaryButton) && rightPrimaryButton)
			{
				foreach (GameObject avatarMotionDestination in this.avatarMotionDestinations)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: avatarMotionDestination,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPressA()
					);
				}

				Debug.Log("Pressed A button");
			}

//			if(SteamVR_Actions.sigverse_PressNearButton.GetStateDown(SteamVR_Input_Sources.LeftHand))
			if(this.leftHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool leftPrimaryButton) && leftPrimaryButton)
			{
				foreach (GameObject avatarMotionDestination in this.avatarMotionDestinations)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: avatarMotionDestination,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPressX()
					);
				}

				Debug.Log("Pressed X button");
			}
		}
		void OnDestroy()
		{
			var xrManagerSettings = XRGeneralSettings.Instance.Manager;

			if (xrManagerSettings == null) { return; }

			if(xrManagerSettings.activeLoader != null)
			{
				xrManagerSettings.activeLoader.Stop();
				XRGeneralSettings.Instance.Manager.DeinitializeLoader();
			}
		}
	}
}
