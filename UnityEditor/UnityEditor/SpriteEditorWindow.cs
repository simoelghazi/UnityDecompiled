using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Experimental.U2D;
using UnityEditor.Sprites;
using UnityEditor.U2D;
using UnityEditor.U2D.Interface;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.U2D.Interface;

namespace UnityEditor
{
	internal class SpriteEditorWindow : SpriteUtilityWindow, ISpriteEditor
	{
		private class SpriteEditorWindowStyles
		{
			public static readonly GUIContent editingDisableMessageLabel = EditorGUIUtility.TrTextContent("Editing is disabled during play mode", null, null);

			public static readonly GUIContent revertButtonLabel = EditorGUIUtility.TrTextContent("Revert", null, null);

			public static readonly GUIContent applyButtonLabel = EditorGUIUtility.TrTextContent("Apply", null, null);

			public static readonly GUIContent spriteEditorWindowTitle = EditorGUIUtility.TrTextContent("Sprite Editor", null, null);

			public static readonly GUIContent pendingChangesDialogContent = EditorGUIUtility.TrTextContent("The asset was modified outside of Sprite Editor Window.\nDo you want to apply pending changes?", null, null);

			public static readonly GUIContent applyRevertDialogTitle = EditorGUIUtility.TrTextContent("Unapplied import settings", null, null);

			public static readonly GUIContent applyRevertDialogContent = EditorGUIUtility.TrTextContent("Unapplied import settings for '{0}'", null, null);

			public static readonly GUIContent noSelectionWarning = EditorGUIUtility.TrTextContent("No texture or sprite selected", null, null);

			public static readonly GUIContent noModuleWarning = EditorGUIUtility.TrTextContent("No Sprite Editor module available", null, null);

			public static readonly GUIContent applyRevertModuleDialogTitle = EditorGUIUtility.TrTextContent("Unapplied module changes", null, null);

			public static readonly GUIContent applyRevertModuleDialogContent = EditorGUIUtility.TrTextContent("You have unapplied changes from the current module", null, null);

			public static readonly GUIContent loadProgressTitle = EditorGUIUtility.TrTextContent("Loading", null, null);

			public static readonly GUIContent loadContentText = EditorGUIUtility.TrTextContent("Loading Sprites {0}/{1}", null, null);
		}

		internal class PreviewTexture2D : UnityEngine.U2D.Interface.Texture2D
		{
			private int m_ActualWidth = 0;

			private int m_ActualHeight = 0;

			public override int width
			{
				get
				{
					return this.m_ActualWidth;
				}
			}

			public override int height
			{
				get
				{
					return this.m_ActualHeight;
				}
			}

			public PreviewTexture2D(UnityEngine.Texture2D t, int width, int height) : base(t)
			{
				this.m_ActualWidth = width;
				this.m_ActualHeight = height;
			}
		}

		private const float k_MarginForFraming = 0.05f;

		private const float k_WarningMessageWidth = 250f;

		private const float k_WarningMessageHeight = 40f;

		private const float k_ModuleListWidth = 90f;

		public static SpriteEditorWindow s_Instance;

		public bool m_ResetOnNextRepaint;

		private List<SpriteRect> m_RectsCache;

		private ISpriteEditorDataProvider m_SpriteDataProvider;

		private bool m_RequestRepaint = false;

		public static bool s_OneClickDragStarted = false;

		public string m_SelectedAssetPath;

		private IEventSystem m_EventSystem;

		private IUndoSystem m_UndoSystem;

		private IAssetDatabase m_AssetDatabase;

		private IGUIUtility m_GUIUtility;

		private UnityEngine.Texture2D m_OutlineTexture;

		private UnityEngine.Texture2D m_ReadableTexture;

		private Dictionary<Type, RequireSpriteDataProviderAttribute> m_ModuleRequireSpriteDataProvider = new Dictionary<Type, RequireSpriteDataProviderAttribute>();

		[SerializeField]
		private string m_SelectedSpriteRectGUID;

		private GUIContent[] m_RegisteredModuleNames;

		private List<SpriteEditorModuleBase> m_AllRegisteredModules;

		private List<SpriteEditorModuleBase> m_RegisteredModules;

		private SpriteEditorModuleBase m_CurrentModule = null;

		private int m_CurrentModuleIndex = 0;

		private Rect warningMessageRect
		{
			get
			{
				return new Rect(base.position.width - 250f - 8f - 16f, 24f, 250f, 40f);
			}
		}

		private bool multipleSprites
		{
			get
			{
				return this.spriteImportMode == SpriteImportMode.Multiple;
			}
		}

		private bool validSprite
		{
			get
			{
				return this.spriteImportMode != SpriteImportMode.None;
			}
		}

		public SpriteImportMode spriteImportMode
		{
			get
			{
				return (this.m_SpriteDataProvider != null) ? this.m_SpriteDataProvider.spriteImportMode : SpriteImportMode.None;
			}
		}

		private bool activeDataProviderSelected
		{
			get
			{
				return this.m_SpriteDataProvider != null;
			}
		}

		public bool textureIsDirty
		{
			get;
			set;
		}

		public bool selectedProviderChanged
		{
			get
			{
				return this.m_SelectedAssetPath != this.GetSelectionAssetPath();
			}
		}

		internal List<SpriteEditorModuleBase> activatedModules
		{
			get
			{
				return this.m_RegisteredModules;
			}
		}

		public List<SpriteRect> spriteRects
		{
			set
			{
				this.m_RectsCache = value;
			}
		}

		public SpriteRect selectedSpriteRect
		{
			get
			{
				SpriteRect result;
				if (this.editingDisabled || this.m_RectsCache == null || string.IsNullOrEmpty(this.m_SelectedSpriteRectGUID))
				{
					result = null;
				}
				else
				{
					GUID guid = new GUID(this.m_SelectedSpriteRectGUID);
					result = this.m_RectsCache.FirstOrDefault((SpriteRect x) => x.spriteID == guid);
				}
				return result;
			}
			set
			{
				this.m_SelectedSpriteRectGUID = ((value != null) ? value.spriteID.ToString() : null);
			}
		}

		public ISpriteEditorDataProvider spriteEditorDataProvider
		{
			get
			{
				return this.m_SpriteDataProvider;
			}
		}

		public bool enableMouseMoveEvent
		{
			set
			{
				base.wantsMouseMove = value;
			}
		}

		public Rect windowDimension
		{
			get
			{
				return this.m_TextureViewRect;
			}
		}

		public ITexture2D previewTexture
		{
			get
			{
				return this.m_Texture;
			}
		}

		public bool editingDisabled
		{
			get
			{
				return EditorApplication.isPlayingOrWillChangePlaymode;
			}
		}

		public SpriteEditorWindow()
		{
			this.m_EventSystem = new EventSystem();
			this.m_UndoSystem = new UndoSystem();
			this.m_AssetDatabase = new AssetDatabaseSystem();
			this.m_GUIUtility = new GUIUtilitySystem();
		}

		public static void GetWindow()
		{
			EditorWindow.GetWindow<SpriteEditorWindow>();
		}

		private void ModifierKeysChanged()
		{
			if (EditorWindow.focusedWindow == this)
			{
				base.Repaint();
			}
		}

		private void OnFocus()
		{
			if (this.selectedProviderChanged)
			{
				this.OnSelectionChange();
			}
		}

		public void RefreshPropertiesCache()
		{
			this.m_SelectedAssetPath = this.GetSelectionAssetPath();
			this.m_SpriteDataProvider = (AssetImporter.GetAtPath(this.m_SelectedAssetPath) as ISpriteEditorDataProvider);
			if (this.m_SpriteDataProvider != null)
			{
				this.m_SpriteDataProvider.InitSpriteEditorDataProvider();
				ITextureDataProvider dataProvider = this.m_SpriteDataProvider.GetDataProvider<ITextureDataProvider>();
				if (dataProvider != null)
				{
					int width = 0;
					int height = 0;
					dataProvider.GetTextureActualWidthAndHeight(out width, out height);
					this.m_Texture = ((!(dataProvider.texture == null)) ? new SpriteEditorWindow.PreviewTexture2D(dataProvider.texture, width, height) : null);
				}
			}
		}

		internal string GetSelectionAssetPath()
		{
			UnityEngine.Object o = Selection.activeObject;
			if (Selection.activeGameObject)
			{
				if (Selection.activeGameObject.GetComponent<SpriteRenderer>())
				{
					if (Selection.activeGameObject.GetComponent<SpriteRenderer>().sprite)
					{
						o = UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(Selection.activeGameObject.GetComponent<SpriteRenderer>().sprite, false);
					}
				}
			}
			return this.m_AssetDatabase.GetAssetPath(o);
		}

		public void InvalidatePropertiesCache()
		{
			this.m_RectsCache = null;
			this.m_SpriteDataProvider = null;
		}

		public bool IsEditingDisabled()
		{
			return EditorApplication.isPlayingOrWillChangePlaymode;
		}

		private void OnSelectionChange()
		{
			string selectionAssetPath = this.GetSelectionAssetPath();
			AssetImporter atPath = AssetImporter.GetAtPath(selectionAssetPath);
			ISpriteEditorDataProvider spriteEditorDataProvider = atPath as ISpriteEditorDataProvider;
			if (spriteEditorDataProvider == null || this.selectedProviderChanged)
			{
				this.HandleApplyRevertDialog(SpriteEditorWindow.SpriteEditorWindowStyles.applyRevertDialogTitle.text, string.Format(SpriteEditorWindow.SpriteEditorWindowStyles.applyRevertDialogContent.text, this.m_SelectedAssetPath));
				this.ResetWindow();
				this.RefreshPropertiesCache();
				this.RefreshRects();
			}
			if (this.m_RectsCache != null)
			{
				if (Selection.activeObject is Sprite)
				{
					this.UpdateSelectedSpriteRect(Selection.activeObject as Sprite);
				}
				else if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<SpriteRenderer>())
				{
					Sprite sprite = Selection.activeGameObject.GetComponent<SpriteRenderer>().sprite;
					this.UpdateSelectedSpriteRect(sprite);
				}
			}
			this.UpdateAvailableModules();
			base.Repaint();
		}

		public void ResetWindow()
		{
			this.InvalidatePropertiesCache();
			this.textureIsDirty = false;
			this.m_Zoom = -1f;
		}

		private void OnEnable()
		{
			base.minSize = new Vector2(360f, 200f);
			base.titleContent = SpriteEditorWindow.SpriteEditorWindowStyles.spriteEditorWindowTitle;
			SpriteEditorWindow.s_Instance = this;
			this.m_UndoSystem.RegisterUndoCallback(new Undo.UndoRedoCallback(this.UndoRedoPerformed));
			EditorApplication.modifierKeysChanged = (EditorApplication.CallbackFunction)Delegate.Combine(EditorApplication.modifierKeysChanged, new EditorApplication.CallbackFunction(this.ModifierKeysChanged));
			this.ResetWindow();
			this.RefreshPropertiesCache();
			this.RefreshRects();
			this.InitModules();
		}

		private void UndoRedoPerformed()
		{
			if (this.selectedProviderChanged)
			{
				this.OnSelectionChange();
			}
			this.InitSelectedSpriteRect();
			base.Repaint();
		}

		private void InitSelectedSpriteRect()
		{
			SpriteRect selectedSpriteRect = null;
			if (this.m_RectsCache != null && this.m_RectsCache.Count > 0)
			{
				if (this.selectedSpriteRect != null)
				{
					selectedSpriteRect = ((this.m_RectsCache.FirstOrDefault((SpriteRect x) => x.spriteID == this.selectedSpriteRect.spriteID) == null) ? this.m_RectsCache[0] : this.selectedSpriteRect);
				}
				else
				{
					selectedSpriteRect = this.m_RectsCache[0];
				}
			}
			this.selectedSpriteRect = selectedSpriteRect;
		}

		private void OnDisable()
		{
			Undo.undoRedoPerformed = (Undo.UndoRedoCallback)Delegate.Remove(Undo.undoRedoPerformed, new Undo.UndoRedoCallback(this.UndoRedoPerformed));
			this.HandleApplyRevertDialog(SpriteEditorWindow.SpriteEditorWindowStyles.applyRevertDialogTitle.text, string.Format(SpriteEditorWindow.SpriteEditorWindowStyles.applyRevertDialogContent.text, this.m_SelectedAssetPath));
			this.InvalidatePropertiesCache();
			EditorApplication.modifierKeysChanged = (EditorApplication.CallbackFunction)Delegate.Remove(EditorApplication.modifierKeysChanged, new EditorApplication.CallbackFunction(this.ModifierKeysChanged));
			SpriteEditorWindow.s_Instance = null;
			if (this.m_OutlineTexture != null)
			{
				UnityEngine.Object.DestroyImmediate(this.m_OutlineTexture);
				this.m_OutlineTexture = null;
			}
			if (this.m_ReadableTexture)
			{
				UnityEngine.Object.DestroyImmediate(this.m_ReadableTexture);
				this.m_ReadableTexture = null;
			}
			if (this.m_CurrentModule != null)
			{
				this.m_CurrentModule.OnModuleDeactivate();
			}
		}

		private void HandleApplyRevertDialog(string dialogTitle, string dialogContent)
		{
			if (this.textureIsDirty && this.m_SpriteDataProvider != null)
			{
				if (EditorUtility.DisplayDialog(dialogTitle, dialogContent, SpriteEditorWindow.SpriteEditorWindowStyles.applyButtonLabel.text, SpriteEditorWindow.SpriteEditorWindowStyles.revertButtonLabel.text))
				{
					this.DoApply();
				}
				else
				{
					this.DoRevert();
				}
				this.SetupModule(this.m_CurrentModuleIndex);
			}
		}

		private void RefreshRects()
		{
			this.m_RectsCache = null;
			if (this.m_SpriteDataProvider != null)
			{
				this.m_RectsCache = this.m_SpriteDataProvider.GetSpriteRects().ToList<SpriteRect>();
			}
			this.InitSelectedSpriteRect();
		}

		private void OnGUI()
		{
			base.InitStyles();
			if (this.m_ResetOnNextRepaint || this.selectedProviderChanged || this.m_RectsCache == null)
			{
				this.HandleApplyRevertDialog(SpriteEditorWindow.SpriteEditorWindowStyles.applyRevertDialogTitle.text, SpriteEditorWindow.SpriteEditorWindowStyles.pendingChangesDialogContent.text);
				this.ResetWindow();
				this.RefreshPropertiesCache();
				this.RefreshRects();
				this.UpdateAvailableModules();
				this.SetupModule(this.m_CurrentModuleIndex);
				this.m_ResetOnNextRepaint = false;
			}
			Matrix4x4 matrix = Handles.matrix;
			if (!this.activeDataProviderSelected)
			{
				using (new EditorGUI.DisabledScope(true))
				{
					GUILayout.Label(SpriteEditorWindow.SpriteEditorWindowStyles.noSelectionWarning, new GUILayoutOption[0]);
				}
			}
			else if (this.m_CurrentModule == null)
			{
				using (new EditorGUI.DisabledScope(true))
				{
					GUILayout.Label(SpriteEditorWindow.SpriteEditorWindowStyles.noModuleWarning, new GUILayoutOption[0]);
				}
			}
			else
			{
				this.DoToolbarGUI();
				base.DoTextureGUI();
				this.DoEditingDisabledMessage();
				this.m_CurrentModule.DoPostGUI();
				Handles.matrix = matrix;
				if (this.m_RequestRepaint)
				{
					base.Repaint();
					this.m_RequestRepaint = false;
				}
			}
		}

		protected override void DoTextureGUIExtras()
		{
			this.HandleFrameSelected();
			if (this.m_EventSystem.current.type == EventType.Repaint)
			{
				SpriteEditorUtility.BeginLines(new Color(1f, 1f, 1f, 0.5f));
				SpriteRect expr_42 = this.selectedSpriteRect;
				GUID? gUID = (expr_42 != null) ? new GUID?(expr_42.spriteID) : null;
				for (int i = 0; i < this.m_RectsCache.Count; i++)
				{
					if (this.m_RectsCache[i].spriteID != gUID)
					{
						SpriteEditorUtility.DrawBox(this.m_RectsCache[i].rect);
					}
				}
				SpriteEditorUtility.EndLines();
			}
			this.m_CurrentModule.DoMainGUI();
		}

		private void DoToolbarGUI()
		{
			GUIStyle toolbar = EditorStyles.toolbar;
			Rect rect = new Rect(0f, 0f, base.position.width, 17f);
			if (this.m_EventSystem.current.type == EventType.Repaint)
			{
				toolbar.Draw(rect, false, false, false, false);
			}
			this.m_TextureViewRect = new Rect(0f, 17f, base.position.width - 16f, base.position.height - 16f - 17f);
			if (this.m_RegisteredModules.Count > 1)
			{
				float num = 90f / base.minSize.x;
				float num2 = (base.position.width <= base.minSize.x) ? 90f : (base.position.width * num);
				num2 = Mathf.Min(num2, EditorStyles.popup.CalcSize(this.m_RegisteredModuleNames[this.m_CurrentModuleIndex]).x);
				int num3 = EditorGUI.Popup(new Rect(0f, 0f, num2, 17f), this.m_CurrentModuleIndex, this.m_RegisteredModuleNames, EditorStyles.toolbarPopup);
				if (num3 != this.m_CurrentModuleIndex)
				{
					if (this.textureIsDirty)
					{
						if (EditorUtility.DisplayDialog(SpriteEditorWindow.SpriteEditorWindowStyles.applyRevertModuleDialogTitle.text, SpriteEditorWindow.SpriteEditorWindowStyles.applyRevertModuleDialogContent.text, SpriteEditorWindow.SpriteEditorWindowStyles.applyButtonLabel.text, SpriteEditorWindow.SpriteEditorWindowStyles.revertButtonLabel.text))
						{
							this.DoApply();
						}
						else
						{
							this.DoRevert();
						}
					}
					this.SetupModule(num3);
				}
				rect.x = num2;
			}
			rect = base.DoAlphaZoomToolbarGUI(rect);
			Rect position = rect;
			position.x = position.width;
			using (new EditorGUI.DisabledScope(!this.textureIsDirty))
			{
				position.width = EditorStyles.toolbarButton.CalcSize(SpriteEditorWindow.SpriteEditorWindowStyles.applyButtonLabel).x;
				position.x -= position.width;
				if (GUI.Button(position, SpriteEditorWindow.SpriteEditorWindowStyles.applyButtonLabel, EditorStyles.toolbarButton))
				{
					this.DoApply();
					this.SetupModule(this.m_CurrentModuleIndex);
				}
				position.width = EditorStyles.toolbarButton.CalcSize(SpriteEditorWindow.SpriteEditorWindowStyles.revertButtonLabel).x;
				position.x -= position.width;
				if (GUI.Button(position, SpriteEditorWindow.SpriteEditorWindowStyles.revertButtonLabel, EditorStyles.toolbarButton))
				{
					this.DoRevert();
					this.SetupModule(this.m_CurrentModuleIndex);
				}
			}
			rect.width = position.x - rect.x;
			this.m_CurrentModule.DoToolbarGUI(rect);
		}

		private void DoEditingDisabledMessage()
		{
			if (this.IsEditingDisabled())
			{
				GUILayout.BeginArea(this.warningMessageRect);
				EditorGUILayout.HelpBox(SpriteEditorWindow.SpriteEditorWindowStyles.editingDisableMessageLabel.text, MessageType.Warning);
				GUILayout.EndArea();
			}
		}

		private void DoApply()
		{
			bool flag = true;
			if (this.m_CurrentModule != null)
			{
				flag = this.m_CurrentModule.ApplyRevert(true);
			}
			this.m_SpriteDataProvider.Apply();
			bool @bool = EditorPrefs.GetBool("VerifySavingAssets", false);
			EditorPrefs.SetBool("VerifySavingAssets", false);
			AssetDatabase.ForceReserializeAssets(new string[]
			{
				this.m_SelectedAssetPath
			}, ForceReserializeAssetsOptions.ReserializeMetadata);
			EditorPrefs.SetBool("VerifySavingAssets", @bool);
			if (flag)
			{
				this.DoTextureReimport(this.m_SelectedAssetPath);
			}
			base.Repaint();
			this.textureIsDirty = false;
			this.InitSelectedSpriteRect();
		}

		private void DoRevert()
		{
			this.textureIsDirty = false;
			this.RefreshRects();
			GUI.FocusControl("");
			if (this.m_CurrentModule != null)
			{
				this.m_CurrentModule.ApplyRevert(false);
			}
		}

		public bool HandleSpriteSelection()
		{
			bool flag = false;
			if (this.m_EventSystem.current.type == EventType.MouseDown && this.m_EventSystem.current.button == 0 && GUIUtility.hotControl == 0 && !this.m_EventSystem.current.alt)
			{
				SpriteRect selectedSpriteRect = this.selectedSpriteRect;
				SpriteRect spriteRect = this.TrySelect(this.m_EventSystem.current.mousePosition);
				if (spriteRect != selectedSpriteRect)
				{
					Undo.RegisterCompleteObjectUndo(this, "Sprite Selection");
					this.selectedSpriteRect = spriteRect;
					flag = true;
				}
				if (this.selectedSpriteRect != null)
				{
					SpriteEditorWindow.s_OneClickDragStarted = true;
				}
				else
				{
					this.RequestRepaint();
				}
				if (flag && this.selectedSpriteRect != null)
				{
					this.m_EventSystem.current.Use();
				}
			}
			return flag;
		}

		private void HandleFrameSelected()
		{
			IEvent current = this.m_EventSystem.current;
			if ((current.type == EventType.ValidateCommand || current.type == EventType.ExecuteCommand) && current.commandName == "FrameSelected")
			{
				if (current.type == EventType.ExecuteCommand)
				{
					if (this.selectedSpriteRect == null)
					{
						return;
					}
					Rect rect = this.selectedSpriteRect.rect;
					float zoom = this.m_Zoom;
					if (rect.width < rect.height)
					{
						zoom = this.m_TextureViewRect.height / (rect.height + this.m_TextureViewRect.height * 0.05f);
					}
					else
					{
						zoom = this.m_TextureViewRect.width / (rect.width + this.m_TextureViewRect.width * 0.05f);
					}
					this.m_Zoom = zoom;
					this.m_ScrollPosition.x = (rect.center.x - (float)this.m_Texture.width * 0.5f) * this.m_Zoom;
					this.m_ScrollPosition.y = (rect.center.y - (float)this.m_Texture.height * 0.5f) * this.m_Zoom * -1f;
					base.Repaint();
				}
				current.Use();
			}
		}

		private void UpdateSelectedSpriteRect(Sprite sprite)
		{
			GUID spriteID = sprite.GetSpriteID();
			for (int i = 0; i < this.m_RectsCache.Count; i++)
			{
				if (spriteID == this.m_RectsCache[i].spriteID)
				{
					this.selectedSpriteRect = this.m_RectsCache[i];
					return;
				}
			}
			this.selectedSpriteRect = null;
		}

		private SpriteRect TrySelect(Vector2 mousePosition)
		{
			float num = 3.40282347E+38f;
			SpriteRect spriteRect = null;
			mousePosition = Handles.inverseMatrix.MultiplyPoint(mousePosition);
			SpriteRect result;
			for (int i = 0; i < this.m_RectsCache.Count; i++)
			{
				SpriteRect spriteRect2 = this.m_RectsCache[i];
				if (spriteRect2.rect.Contains(mousePosition))
				{
					if (spriteRect2 == this.selectedSpriteRect)
					{
						result = spriteRect2;
						return result;
					}
					float width = spriteRect2.rect.width;
					float height = spriteRect2.rect.height;
					float num2 = width * height;
					if (width > 0f && height > 0f && num2 < num)
					{
						spriteRect = spriteRect2;
						num = num2;
					}
				}
			}
			result = spriteRect;
			return result;
		}

		public void DoTextureReimport(string path)
		{
			if (this.m_SpriteDataProvider != null)
			{
				try
				{
					AssetDatabase.StartAssetEditing();
					AssetDatabase.ImportAsset(path);
				}
				finally
				{
					AssetDatabase.StopAssetEditing();
				}
			}
		}

		private void SetupModule(int newModuleIndex)
		{
			if (!(SpriteEditorWindow.s_Instance == null))
			{
				if (this.m_CurrentModule != null)
				{
					this.m_CurrentModule.OnModuleDeactivate();
				}
				this.m_CurrentModule = null;
				if (this.m_RegisteredModules.Count > newModuleIndex)
				{
					this.m_CurrentModule = this.m_RegisteredModules[newModuleIndex];
					this.m_CurrentModule.OnModuleActivate();
					this.m_CurrentModuleIndex = newModuleIndex;
				}
			}
		}

		private void UpdateAvailableModules()
		{
			if (this.m_AllRegisteredModules != null)
			{
				this.m_RegisteredModules = new List<SpriteEditorModuleBase>();
				foreach (SpriteEditorModuleBase current in this.m_AllRegisteredModules)
				{
					if (current.CanBeActivated())
					{
						RequireSpriteDataProviderAttribute requireSpriteDataProviderAttribute = null;
						this.m_ModuleRequireSpriteDataProvider.TryGetValue(current.GetType(), out requireSpriteDataProviderAttribute);
						if (requireSpriteDataProviderAttribute == null || requireSpriteDataProviderAttribute.ContainsAllType(this.m_SpriteDataProvider))
						{
							this.m_RegisteredModules.Add(current);
						}
					}
				}
				this.m_RegisteredModuleNames = new GUIContent[this.m_RegisteredModules.Count];
				for (int i = 0; i < this.m_RegisteredModules.Count; i++)
				{
					this.m_RegisteredModuleNames[i] = new GUIContent(this.m_RegisteredModules[i].moduleName);
				}
				if (!this.m_RegisteredModules.Contains(this.m_CurrentModule))
				{
					this.SetupModule(0);
				}
				else
				{
					this.SetupModule(this.m_CurrentModuleIndex);
				}
			}
		}

		private void InitModules()
		{
			this.m_AllRegisteredModules = new List<SpriteEditorModuleBase>();
			this.m_ModuleRequireSpriteDataProvider.Clear();
			if (this.m_OutlineTexture == null)
			{
				this.m_OutlineTexture = new UnityEngine.Texture2D(1, 16, TextureFormat.RGBA32, false);
				this.m_OutlineTexture.SetPixels(new Color[]
				{
					new Color(0.5f, 0.5f, 0.5f, 0.5f),
					new Color(0.5f, 0.5f, 0.5f, 0.5f),
					new Color(0.8f, 0.8f, 0.8f, 0.8f),
					new Color(0.8f, 0.8f, 0.8f, 0.8f),
					Color.white,
					Color.white,
					Color.white,
					Color.white,
					new Color(0.8f, 0.8f, 0.8f, 1f),
					new Color(0.5f, 0.5f, 0.5f, 0.8f),
					new Color(0.3f, 0.3f, 0.3f, 0.5f),
					new Color(0.3f, 0.3f, 0.3f, 0.5f),
					new Color(0.3f, 0.3f, 0.3f, 0.3f),
					new Color(0.3f, 0.3f, 0.3f, 0.3f),
					new Color(0.1f, 0.1f, 0.1f, 0.1f),
					new Color(0.1f, 0.1f, 0.1f, 0.1f)
				});
				this.m_OutlineTexture.Apply();
				this.m_OutlineTexture.hideFlags = HideFlags.HideAndDontSave;
			}
			UnityEngine.U2D.Interface.Texture2D outlineTexture = new UnityEngine.U2D.Interface.Texture2D(this.m_OutlineTexture);
			this.RegisterModule(new SpriteFrameModule(this, this.m_EventSystem, this.m_UndoSystem, this.m_AssetDatabase));
			this.RegisterModule(new SpritePolygonModeModule(this, this.m_EventSystem, this.m_UndoSystem, this.m_AssetDatabase));
			this.RegisterModule(new SpriteOutlineModule(this, this.m_EventSystem, this.m_UndoSystem, this.m_AssetDatabase, this.m_GUIUtility, new ShapeEditorFactory(), outlineTexture));
			this.RegisterModule(new SpritePhysicsShapeModule(this, this.m_EventSystem, this.m_UndoSystem, this.m_AssetDatabase, this.m_GUIUtility, new ShapeEditorFactory(), outlineTexture));
			this.RegisterCustomModules();
			this.UpdateAvailableModules();
		}

		private void RegisterModule(SpriteEditorModuleBase module)
		{
			Type type = module.GetType();
			object[] customAttributes = type.GetCustomAttributes(typeof(RequireSpriteDataProviderAttribute), false);
			if (customAttributes.Length == 1)
			{
				this.m_ModuleRequireSpriteDataProvider.Add(type, (RequireSpriteDataProviderAttribute)customAttributes[0]);
			}
			this.m_AllRegisteredModules.Add(module);
		}

		private void RegisterCustomModules()
		{
			Type typeFromHandle = typeof(SpriteEditorModuleBase);
			foreach (Type current in EditorAssemblies.SubclassesOf(typeFromHandle))
			{
				if (!current.IsAbstract)
				{
					bool flag = false;
					foreach (SpriteEditorModuleBase current2 in this.m_AllRegisteredModules)
					{
						if (current2.GetType() == current)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						Type[] types = new Type[0];
						ConstructorInfo constructor = current.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.HasThis, types, null);
						if (constructor != null)
						{
							try
							{
								SpriteEditorModuleBase spriteEditorModuleBase = constructor.Invoke(new object[0]) as SpriteEditorModuleBase;
								if (spriteEditorModuleBase != null)
								{
									spriteEditorModuleBase.spriteEditor = this;
									this.RegisterModule(spriteEditorModuleBase);
								}
							}
							catch (Exception ex)
							{
								Debug.LogWarning(string.Concat(new object[]
								{
									"Unable to instantiate custom module ",
									current.FullName,
									". Exception:",
									ex
								}));
							}
						}
						else
						{
							Debug.LogWarning(current.FullName + " does not have a parameterless constructor");
						}
					}
				}
			}
		}

		public void RequestRepaint()
		{
			if (EditorWindow.focusedWindow != this)
			{
				base.Repaint();
			}
			else
			{
				this.m_RequestRepaint = true;
			}
		}

		public void SetDataModified()
		{
			this.textureIsDirty = true;
		}

		public void ApplyOrRevertModification(bool apply)
		{
			if (apply)
			{
				this.DoApply();
			}
			else
			{
				this.DoRevert();
			}
		}

		public T GetDataProvider<T>() where T : class
		{
			return (this.m_SpriteDataProvider != null) ? this.m_SpriteDataProvider.GetDataProvider<T>() : ((T)((object)null));
		}

		internal static void OnTextureReimport(string path)
		{
			if (SpriteEditorWindow.s_Instance != null && SpriteEditorWindow.s_Instance.m_SelectedAssetPath == path)
			{
				SpriteEditorWindow.s_Instance.m_ResetOnNextRepaint = true;
				SpriteEditorWindow.s_Instance.Repaint();
			}
		}
	}
}
