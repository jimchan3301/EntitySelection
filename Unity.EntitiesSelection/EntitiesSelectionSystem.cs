﻿// Author: Jonas De Maeseneer

using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class EntitySelectionSystem : SystemBase
{
    // Instance members
    public RenderTexture _objectIDRenderTarget { get; private set; }
    private Shader _colorIDShader;
    private Texture2D _objectID1x1Texture;

    // Material
    private readonly Dictionary<int, int> _entityIndexToVersion = new Dictionary<int, int>();
    private static readonly int ColorPropertyID = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock _idMaterialPropertyBlock;
    private Material _idMaterial;

    // cached reflection variable to find actual scene view camera rect
    private static readonly PropertyInfo _sceneViewCameraRectProp = typeof(SceneView).GetProperty("cameraRect", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    protected override void OnCreate()
    {
        _colorIDShader = Shader.Find("Unlit/EntityIdShader");
        _objectID1x1Texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _idMaterialPropertyBlock = new MaterialPropertyBlock();

        // On first load of the package the shader can't be found, but OnClicked handles null materials
        if (_colorIDShader)
        {
            _idMaterial = new Material(_colorIDShader);
        }
    }

    private Entity OnClicked(Vector2 point, int renderTextureWidth, int renderTextureHeight, in Matrix4x4 viewMatrix, in Matrix4x4 projectionMatrix)
    {
        // Needs to happen when the scene changed
        if (_idMaterial == null)
        {
            OnCreate();
        }

        // Initial creation + on window resize
        if (_objectIDRenderTarget == null ||
            renderTextureWidth != _objectIDRenderTarget.width ||
            renderTextureHeight != _objectIDRenderTarget.height)
        {
            _objectIDRenderTarget = new RenderTexture(renderTextureWidth, renderTextureHeight, 0)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Point,
                autoGenerateMips = false,
                depth = 24
            };
        }

        // Rendering Unique color per entity
        RenderEntityIDs(viewMatrix, projectionMatrix);
        // Getting the pixel at the mouse position and converting the color to an entity
        return SelectEntity(point);
    }

    private void RenderEntityIDs(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
    {
        var cmd = new CommandBuffer();
        cmd.SetRenderTarget(_objectIDRenderTarget);
        cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));
        cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        Entities.ForEach((Entity e, in RenderMeshArray meshes, in MaterialMeshInfo info, in LocalToWorld localToWorld) =>
        {
            var mesh = meshes.GetMesh(info);
            if (mesh == null)
            {
                return;
            }
            _entityIndexToVersion[e.Index] = e.Version;
            _idMaterialPropertyBlock.SetColor(ColorPropertyID, IndexToColor(e.Index));

            cmd.DrawMesh(mesh, localToWorld.Value, _idMaterial, 0, 0, _idMaterialPropertyBlock);
        })
        // .ScheduleParallel();
        .WithoutBurst()
        .Run();

        Graphics.ExecuteCommandBuffer(cmd);
    }

    private Entity SelectEntity(Vector2 point)
    {
        var selectedEntity = new Entity
        {
            Index = ColorToIndex(GetColorAtPoint(point, _objectIDRenderTarget))
        };
        if (_entityIndexToVersion.ContainsKey(selectedEntity.Index))
        {
            selectedEntity.Version = _entityIndexToVersion[selectedEntity.Index];
        }
        return selectedEntity;
    }

    private Color GetColorAtPoint(Vector2 posLocalToSceneView, RenderTexture objectIdTex)
    {
        RenderTexture.active = objectIdTex;

        // clicked outside of scene view
        if (posLocalToSceneView.x < 0 || posLocalToSceneView.x > objectIdTex.width
            || posLocalToSceneView.y < 0 || posLocalToSceneView.y > objectIdTex.height)
        {
            return new Color(0, 0, 0, 0); // results in Entity.Null
        }

        // handles when the edges of the screen are clicked
        posLocalToSceneView.x = Mathf.Clamp(posLocalToSceneView.x, 0, objectIdTex.width - 1);
        posLocalToSceneView.y = Mathf.Clamp(posLocalToSceneView.y, 0, objectIdTex.height - 1);

        _objectID1x1Texture.ReadPixels(new Rect(posLocalToSceneView.x, posLocalToSceneView.y, 1, 1), 0, 0, false);
        _objectID1x1Texture.Apply();
        RenderTexture.active = null;

        return _objectID1x1Texture.GetPixel(0, 0);
    }

    private static Color32 IndexToColor(int index)
    {
        var bytes = BitConverter.GetBytes(index);
        return new Color32(bytes[0], bytes[1], bytes[2], bytes[3]);
    }

    private static int ColorToIndex(Color32 color)
    {
        var bytes = new byte[] { color.r, color.g, color.b, color.a };
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// Get the entity at a specific point.
    /// </summary>
    /// <param name="world"> The world to use for the selection. </param>
    /// <param name="point"> The point in screen space. </param>
    /// <param name="renderTextureWidth"> The width of the render texture. </param>
    /// <param name="renderTextureHeight"> The height of the render texture. </param>
    /// <param name="viewMatrix"> The view matrix of the camera. </param>
    /// <param name="projectionMatrix"> The projection matrix of the camera. </param>
    /// <example>
    /// <code>
    /// var entity = EntitySelectionSystem.GetEntityAtPoint(
    ///     World.DefaultGameObjectInjectionWorld,
    ///     new Vector2(Input.mousePosition.x, Input.mousePosition.y),
    ///     Camera.main.pixelWidth, Camera.main.pixelHeight,
    ///     Camera.main.worldToCameraMatrix, Camera.main.projectionMatrix
    /// );
    /// </code>
    /// </example>
    /// <returns> The entity at the point, or Entity.Null if no entity was found. </returns>
    public static Entity GetEntityAtPoint(World world, Vector2 point, int renderTextureWidth, int renderTextureHeight, in Matrix4x4 viewMatrix, in Matrix4x4 projectionMatrix)
    {
        var system = world.GetExistingSystemManaged<EntitySelectionSystem>();
        return system?.OnClicked(point, renderTextureWidth, renderTextureHeight, viewMatrix, projectionMatrix) ?? Entity.Null;
    }
    /// <summary>
    /// Get the entity at a specific point.
    /// </summary>
    /// <param name="point"> The point in screen space. </param>
    /// <param name="camera"> The camera to use for the selection. </param>
    /// <example>
    /// <code>
    /// var entity = EntitySelectionSystem.GetEntityAtPoint(new Vector2(Input.mousePosition.x, Input.mousePosition.y), Camera.main);
    /// </code>
    /// <returns> The entity at the point, or Entity.Null if no entity was found. </returns>
    public static Entity GetEntityAtPoint(Vector2 point, Camera camera)
    {
        return GetEntityAtPoint(World.DefaultGameObjectInjectionWorld, point, camera.scaledPixelWidth, camera.scaledPixelHeight, camera.worldToCameraMatrix, camera.projectionMatrix);
    }

    protected override void OnDestroy()
    {
        Object.Destroy(_idMaterial);
        Object.Destroy(_objectID1x1Texture);
    }

    protected override void OnUpdate()
    {
    }
}
