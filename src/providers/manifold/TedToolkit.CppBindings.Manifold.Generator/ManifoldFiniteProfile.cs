// -----------------------------------------------------------------------
// <copyright file="ManifoldFiniteProfile.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>Defines the finite Manifold API as data consumed by Shared's paired emitter.</summary>
internal static class ManifoldFiniteProfile
{
    internal static BindingFiniteProfileApi Create(ManifoldProfile profile)
    {
        var owner = new BindingOwnerDefinition(
            "Manifold",
            "ManifoldAdapter",
            8,
            8,
            "Manifold_Destroy");
        var self = new BindingOwnerParameterDefinition("self", owner.Name, true);
        return new(
            "Manifold.Bindings.g.cs",
            "ManifoldProfileAdapter.cpp",
            [new("ManifoldError", Array.AsReadOnly(profile.Errors.ToArray())),],
            [],
            [owner,],
            [],
            [
                new(
                    "ManifoldMeshData",
                    [
                        new("double[]", "double[]", "VertexCoordinates", "[]", "$value"),
                        new("ulong[]", "ulong[]", "TriangleIndices", "[]", "$value"),
                    ],
                    BindingCompositeResultKind.SealedClass,
                    [new("int", "PositionPropertyCount", "3"),]),
            ],
            [
                new BindingBufferOwnerOperationDefinition(
                    "Create",
                    owner.Name,
                    null,
                    null,
                    null,
                    "void",
                    "0",
                    [
                        new(
                            "vertexCoordinates",
                            "double",
                            "double",
                            3,
                            "Vertex coordinate length must be a multiple of three.",
                            PointerName: "vertexPointer"),
                        new(
                            "triangleIndices",
                            "ulong",
                            "std::uint64_t",
                            3,
                            "Triangle index length must be a multiple of three.",
                            PointerName: "indexPointer"),
                    ],
                    "Manifold_Create",
                    CreateBody),
                new BindingScalarOperationDefinition(
                    "ManifoldExtensions",
                    "Status",
                    "ManifoldError",
                    "int",
                    "11",
                    [self,],
                    [],
                    "Manifold_Status",
                    "return static_cast<int>(self->Value->Status());",
                    ManagedTransportType: "int"),
                new BindingOwnedOperationDefinition(
                    "ManifoldExtensions",
                    "Boolean",
                    owner.Name,
                    [self, new("second", owner.Name),],
                    [
                        new(
                            "operation",
                            "ManifoldOp",
                            "signed char",
                            RequireDefinedEnum: true,
                            ManagedTransportType: "sbyte",
                            ManagedArgumentExpression: "(sbyte)$value"),
                    ],
                    "Manifold_Boolean",
                    BooleanBody),
                new BindingOwnedOperationDefinition(
                    "ManifoldExtensions",
                    "Translate",
                    owner.Name,
                    [self,],
                    [new("x", "double", "double"), new("y", "double", "double"), new("z", "double", "double"),],
                    "Manifold_Translate",
                    "new (result) ManifoldAdapter(self->Value->Translate(manifold::vec3(x, y, z)));"),
                new BindingScalarOperationDefinition(
                    "ManifoldExtensions",
                    "NumTri",
                    "nuint",
                    "std::size_t",
                    "0",
                    [self,],
                    [],
                    "Manifold_NumTri",
                    "return self->Value->NumTri();"),
                new BindingTwoPhaseOperationDefinition(
                    "ManifoldExtensions",
                    "GetMesh",
                    "ManifoldMeshData",
                    self,
                    [
                        new("VertexCoordinates", "double", "double", "vertexCoordinateCount", "vertexPointer"),
                        new("TriangleIndices", "ulong", "std::uint64_t", "triangleIndexCount", "indexPointer"),
                    ],
                    "Native mesh exceeds the maximum managed array length.",
                    "Manifold_GetMeshCounts",
                    GetMeshCountsBody,
                    "Manifold_CopyMesh",
                    CopyMeshBody),
            ],
            NativePreamble,
            "Unknown native Manifold exception.",
            [
                "Manifold_NativeError_Clear",
                "Manifold_Destroy",
                "Manifold_Create",
                "Manifold_Status",
                "Manifold_Boolean",
                "Manifold_Translate",
                "Manifold_NumTri",
                "Manifold_GetMeshCounts",
                "Manifold_CopyMesh",
            ],
            []);
    }

    internal static EnumModel CreateOperationEnum(ManifoldProfile profile)
    {
        return new()
        {
            DescriptionItems = [],
            Name = "ManifoldOp",
            SourceType = "manifold::OpType",
            UnderlyingType = new("sbyte"),
            Members = profile.Operations.Select(static member => new EnumMemberModel()
            {
                DescriptionItems = [],
                Name = member.Key,
                Value = member.Value.ToLiteral(),
            }).ToArray(),
        };
    }

    internal static BindingNativeProject CreateNativeProject(ManifoldProfile profile)
    {
        return new(
            "CMakeLists.txt",
            (sources, writer, token) => writer.WriteAsync(
                RenderCMake(profile.NativeLibraryBaseName, sources).AsMemory(), token));
    }

    private const string NativePreamble = """
        #include <manifold/manifold.h>
        #include <manifold/mesh.h>

        #include <algorithm>
        #include <cstddef>
        #include <cstdint>
        #include <cstdlib>
        #include <cstring>
        #include <exception>
        #include <new>
        #include <stdexcept>
        #include <type_traits>
        #include <utility>

        struct ManifoldAdapter
        {
            manifold::Manifold* Value;

            explicit ManifoldAdapter(const manifold::MeshGL64& mesh)
                : Value(new manifold::Manifold(mesh)) {}

            explicit ManifoldAdapter(manifold::Manifold&& value)
                : Value(new manifold::Manifold(std::move(value))) {}

            ~ManifoldAdapter() noexcept { delete Value; }

            ManifoldAdapter(const ManifoldAdapter&) = delete;
            ManifoldAdapter& operator=(const ManifoldAdapter&) = delete;
        };

        static_assert(sizeof(ManifoldAdapter) == 8 && alignof(ManifoldAdapter) == 8);
        static_assert(sizeof(std::size_t) == 8);
        static_assert(std::is_same_v<std::underlying_type_t<manifold::OpType>, char>);
        static_assert(static_cast<int>(manifold::OpType::Add) == 0);
        static_assert(static_cast<int>(manifold::OpType::Subtract) == 1);
        static_assert(static_cast<int>(manifold::OpType::Intersect) == 2);
        static_assert(std::is_same_v<std::underlying_type_t<manifold::Manifold::Error>, int>);
        static_assert(static_cast<int>(manifold::Manifold::Error::NoError) == 0);
        static_assert(static_cast<int>(manifold::Manifold::Error::NonFiniteVertex) == 1);
        static_assert(static_cast<int>(manifold::Manifold::Error::NotManifold) == 2);
        static_assert(static_cast<int>(manifold::Manifold::Error::VertexOutOfBounds) == 3);
        static_assert(static_cast<int>(manifold::Manifold::Error::PropertiesWrongLength) == 4);
        static_assert(static_cast<int>(manifold::Manifold::Error::MissingPositionProperties) == 5);
        static_assert(static_cast<int>(manifold::Manifold::Error::MergeVectorsDifferentLengths) == 6);
        static_assert(static_cast<int>(manifold::Manifold::Error::MergeIndexOutOfBounds) == 7);
        static_assert(static_cast<int>(manifold::Manifold::Error::TransformWrongLength) == 8);
        static_assert(static_cast<int>(manifold::Manifold::Error::RunIndexWrongLength) == 9);
        static_assert(static_cast<int>(manifold::Manifold::Error::FaceIDWrongLength) == 10);
        static_assert(static_cast<int>(manifold::Manifold::Error::InvalidConstruction) == 11);
        static_assert(static_cast<int>(manifold::Manifold::Error::ResultTooLarge) == 12);
        static_assert(static_cast<int>(manifold::Manifold::Error::InvalidTangents) == 13);
        static_assert(static_cast<int>(manifold::Manifold::Error::Cancelled) == 14);
        """;

    private const string CreateBody = """
        manifold::MeshGL64 mesh;
        if (vertexCoordinatesCount != 0)
            mesh.vertProperties.assign(vertexCoordinates, vertexCoordinates + vertexCoordinatesCount);
        if (triangleIndicesCount != 0)
            mesh.triVerts.assign(triangleIndices, triangleIndices + triangleIndicesCount);
        new (result) ManifoldAdapter(mesh);
        """;

    private const string BooleanBody = """
        if (operation < 0 || operation > 2) throw std::out_of_range("operation");
        new (result) ManifoldAdapter(self->Value->Boolean(
            *second->Value, static_cast<manifold::OpType>(operation)));
        """;

    private const string GetMeshCountsBody = """
        const auto mesh = self->Value->GetMeshGL64();
        *vertexCoordinateCount = mesh.vertProperties.size();
        *triangleIndexCount = mesh.triVerts.size();
        """;

    private const string CopyMeshBody = """
        const auto mesh = self->Value->GetMeshGL64();
        if (mesh.vertProperties.size() != vertexCoordinateCount || mesh.triVerts.size() != triangleIndexCount)
            throw std::invalid_argument("Mesh output length changed between count and copy.");
        if (vertexCoordinateCount != 0)
            std::copy(mesh.vertProperties.begin(), mesh.vertProperties.end(), vertexPointer);
        if (triangleIndexCount != 0)
            std::copy(mesh.triVerts.begin(), mesh.triVerts.end(), indexPointer);
        """;

    private static string RenderCMake(string libraryBaseName, IReadOnlyList<string> sources)
    {
        var sourceList = string.Join(" ", sources);
        return $$"""
                 cmake_minimum_required(VERSION 3.28)
                 project(TedToolkitCppBindingsManifold LANGUAGES CXX)

                 file(WRITE "${CMAKE_BINARY_DIR}/compiler-identity.txt"
                     "${CMAKE_CXX_COMPILER_ID}|${CMAKE_CXX_COMPILER_VERSION}|${CMAKE_CXX_COMPILER}")

                 find_package(manifold CONFIG REQUIRED)

                 add_library({{libraryBaseName}} SHARED {{sourceList}})
                 target_compile_features({{libraryBaseName}} PRIVATE cxx_std_20)
                 target_compile_options({{libraryBaseName}} PRIVATE /MP1)
                 target_link_libraries({{libraryBaseName}} PRIVATE manifold::manifold)
                 """;
    }
}