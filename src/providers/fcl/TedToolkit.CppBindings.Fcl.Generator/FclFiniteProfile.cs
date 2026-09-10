// -----------------------------------------------------------------------
// <copyright file="FclFiniteProfile.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Defines the finite FCL API as data consumed by Shared's paired emitter.</summary>
internal static class FclFiniteProfile
{
    internal static BindingFiniteProfileApi Create(FclProfile profile)
    {
        var status = new BindingStatusDefinition(
            "FclBvhReturnCode",
            Array.AsReadOnly(profile.BvhReturnCodes.ToArray()));
        var value = new BindingValueTypeDefinition(
            "FclVector3",
            "FclVector3Transport",
            24,
            8,
            [new("double", "X"), new("double", "Y"), new("double", "Z"),]);
        var owner = new BindingOwnerDefinition(
            "FclBvhModel",
            "FclBvhModelAdapter",
            8,
            8,
            "Fcl_Model_Destroy");
        var factory = new BindingBufferOwnerOperationDefinition(
            "Create",
            owner.Name,
            "FclModelBuildResult",
            status.Name,
            "BVH_OK",
            "int",
            "fcl::BVH_ERR_UNKNOWN",
            [
                new(
                    "vertexCoordinates",
                    "double",
                    "double",
                    3,
                    "Vertex coordinate length must be a multiple of three."),
                new(
                    "triangleIndices",
                    "nuint",
                    "std::size_t",
                    3,
                    "Triangle index length must be a multiple of three.",
                    true,
                    "A triangle index must be less than the vertex count."),
            ],
            "Fcl_Model_Create",
            FactoryBody);
        var compositeResult = new BindingCompositeResultDefinition(
            "FclContinuousCollisionResult",
            [
                new("byte", "bool", "IsCollide", "0", "$value != 0"),
                new("double", "double", "TimeOfContact", "1", "$value"),
            ]);
        return new(
            "Fcl.Bindings.g.cs",
            "FclProfileAdapter.cpp",
            [status],
            [value],
            [owner],
            [new("FclModelBuildResult", status.Name, owner.Name, "Code", "Model")],
            [compositeResult],
            [
                factory,
                new BindingCompositeOperationDefinition(
                    "FclContinuousCollision",
                    "Query",
                    compositeResult.Name,
                    [new("first", owner.Name), new("second", owner.Name)],
                    [new("secondTranslation", value.Name, value.NativeName, true)],
                    "Fcl_ContinuousCollision_Query",
                    CompositeOperationBody),
            ],
            NativePreamble,
            "Unknown native FCL exception.",
            [
                "Fcl_NativeError_Clear",
                "Fcl_Model_Destroy",
                "Fcl_Model_Create",
                "Fcl_ContinuousCollision_Query",
                "Fcl_Lifetime_Reset",
                "Fcl_Lifetime_CreateCount",
                "Fcl_Lifetime_DestroyCount",
            ],
            [
                new("Fcl_Lifetime_Reset", "void", "", "ModelCreateCount = 0; ModelDestroyCount = 0;"),
                new("Fcl_Lifetime_CreateCount", "int", "", "return ModelCreateCount.load();"),
                new("Fcl_Lifetime_DestroyCount", "int", "", "return ModelDestroyCount.load();"),
            ]);
    }

    internal static BindingNativeProject CreateNativeProject(FclProfile profile)
    {
        return new(
            "CMakeLists.txt",
            (sources, writer, token) => writer.WriteAsync(
                RenderCMake(profile.NativeLibraryBaseName, sources).AsMemory(), token));
    }

    private const string NativePreamble = """
        #include <fcl/fcl.h>

        #include <atomic>
        #include <cstddef>
        #include <cstdint>
        #include <cstdlib>
        #include <cstring>
        #include <exception>
        #include <memory>
        #include <new>
        #include <stdexcept>
        #include <type_traits>
        #include <utility>
        #include <vector>

        struct FclVector3Transport
        {
            double X;
            double Y;
            double Z;
        };

        using Model = fcl::BVHModel<fcl::OBBRSS<double>>;
        static std::atomic<int> ModelCreateCount = 0;
        static std::atomic<int> ModelDestroyCount = 0;

        struct CountedModel
        {
            std::unique_ptr<Model> Value;

            CountedModel() : Value(std::make_unique<Model>()) { ++ModelCreateCount; }
            ~CountedModel() noexcept { if (Value) ++ModelDestroyCount; }
        };

        struct FclBvhModelAdapter
        {
            Model* Value;

            explicit FclBvhModelAdapter(CountedModel&& value) noexcept : Value(value.Value.release()) {}
            ~FclBvhModelAdapter() noexcept { delete Value; ++ModelDestroyCount; }
            FclBvhModelAdapter(const FclBvhModelAdapter&) = delete;
            FclBvhModelAdapter& operator=(const FclBvhModelAdapter&) = delete;
        };

        static_assert(sizeof(FclVector3Transport) == 24 && alignof(FclVector3Transport) == 8);
        static_assert(offsetof(FclVector3Transport, X) == 0);
        static_assert(offsetof(FclVector3Transport, Y) == 8);
        static_assert(offsetof(FclVector3Transport, Z) == 16);
        static_assert(sizeof(fcl::Vector3<double>) == 24 && alignof(fcl::Vector3<double>) == 8);
        static_assert(sizeof(FclBvhModelAdapter) == 8 && alignof(FclBvhModelAdapter) == 8);
        static_assert(sizeof(std::size_t) == 8);
        static_assert(std::is_same_v<std::underlying_type_t<fcl::BVHReturnCode>, int>);
        static_assert(fcl::BVH_OK == 0);
        static_assert(fcl::BVH_ERR_MODEL_OUT_OF_MEMORY == -1);
        static_assert(fcl::BVH_ERR_BUILD_OUT_OF_SEQUENCE == -2);
        static_assert(fcl::BVH_ERR_BUILD_EMPTY_MODEL == -3);
        static_assert(fcl::BVH_ERR_BUILD_EMPTY_PREVIOUS_FRAME == -4);
        static_assert(fcl::BVH_ERR_UNSUPPORTED_FUNCTION == -5);
        static_assert(fcl::BVH_ERR_UNUPDATED_MODEL == -6);
        static_assert(fcl::BVH_ERR_INCORRECT_DATA == -7);
        static_assert(fcl::BVH_ERR_UNKNOWN == -8);
        """;

    private const string FactoryBody = """
        CountedModel model;
        std::vector<fcl::Vector3<double>> vertices;
        vertices.reserve(vertexCoordinatesCount / 3);
        for (std::size_t index = 0; index < vertexCoordinatesCount; index += 3)
            vertices.emplace_back(
                vertexCoordinates[index],
                vertexCoordinates[index + 1],
                vertexCoordinates[index + 2]);
        std::vector<fcl::Triangle> triangles;
        triangles.reserve(triangleIndicesCount / 3);
        for (std::size_t index = 0; index < triangleIndicesCount; index += 3)
            triangles.emplace_back(
                static_cast<int>(triangleIndices[index]),
                static_cast<int>(triangleIndices[index + 1]),
                static_cast<int>(triangleIndices[index + 2]));

        auto code = model.Value->beginModel();
        if (code == fcl::BVH_OK) code = model.Value->addSubModel(vertices, triangles);
        if (code == fcl::BVH_OK) code = model.Value->endModel();
        if (code != fcl::BVH_OK) return code;
        new (result) FclBvhModelAdapter(std::move(model));
        return static_cast<int>(fcl::BVH_OK);
        """;

    private const string CompositeOperationBody = """
        const fcl::CollisionObject<double> firstObject(
            std::make_shared<Model>(*first->Value), fcl::Transform3<double>::Identity());
        const fcl::CollisionObject<double> secondObject(
            std::make_shared<Model>(*second->Value), fcl::Transform3<double>::Identity());
        fcl::ContinuousCollisionRequest<double> request;
        request.ccd_motion_type = fcl::CCDM_LINEAR;
        request.ccd_solver_type = fcl::CCDC_CONSERVATIVE_ADVANCEMENT;
        fcl::ContinuousCollisionResult<double> result;
        const auto firstEnd = fcl::Transform3<double>::Identity();
        auto secondEnd = fcl::Transform3<double>::Identity();
        secondEnd.translation() = fcl::Vector3<double>(
            secondTranslation->X,
            secondTranslation->Y,
            secondTranslation->Z);
        fcl::continuousCollide(&firstObject, firstEnd, &secondObject, secondEnd, request, result);
        if (!result.is_collide)
        {
            // FCL 0.7's oriented mesh conservative-advancement traversal can normalize a zero
            // separation vector at first contact and lose a pure-translation hit. Confirm only
            // that miss through FCL's exact translation solver; t == 1 remains the endpoint ambiguity.
            auto fallbackRequest = request;
            fallbackRequest.ccd_motion_type = fcl::CCDM_TRANS;
            fallbackRequest.ccd_solver_type = fcl::CCDC_POLYNOMIAL_SOLVER;
            fcl::ContinuousCollisionResult<double> fallbackResult;
            fcl::continuousCollide(
                &firstObject, firstEnd, &secondObject, secondEnd, fallbackRequest, fallbackResult);
            if (fallbackResult.is_collide && fallbackResult.time_of_contact < 1.0)
                result = fallbackResult;
        }

        *isCollide = result.is_collide ? 1 : 0;
        *timeOfContact = result.is_collide ? result.time_of_contact : 1.0;
        """;

    private static string RenderCMake(string libraryBaseName, IReadOnlyList<string> sources)
    {
        var sourceList = string.Join(" ", sources);
        return $$"""
                 cmake_minimum_required(VERSION 3.28)
                 project(TedToolkitCppBindingsFcl LANGUAGES CXX)

                 file(WRITE "${CMAKE_BINARY_DIR}/compiler-identity.txt"
                     "${CMAKE_CXX_COMPILER_ID}|${CMAKE_CXX_COMPILER_VERSION}|${CMAKE_CXX_COMPILER}")

                 find_package(fcl CONFIG REQUIRED)
                 add_library({{libraryBaseName}} SHARED {{sourceList}})
                 target_compile_features({{libraryBaseName}} PRIVATE cxx_std_20)
                 target_compile_options({{libraryBaseName}} PRIVATE /MP1)
                 target_link_libraries({{libraryBaseName}} PRIVATE fcl)
                 """;
    }
}