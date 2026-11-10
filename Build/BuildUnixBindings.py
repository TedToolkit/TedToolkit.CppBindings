"""Generate and build one provider's native library, then stage its NuGet assets."""

import argparse
from collections import deque
from hashlib import file_digest
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys
import tempfile


TARGETS = {
    "linux-x64": ("Linux", "x86_64", "x64-linux", ".so"),
    "osx-arm64": ("Darwin", "arm64", "arm64-osx", ".dylib"),
}
PROVIDER_PORTS = {
    "occt": "opencascade",
    "cgal": "cgal",
    "manifold": "manifold",
    "fcl": "fcl",
    "smlib": None,
}
LINUX_SYSTEM_LIBRARIES = {
    "ld-linux-x86-64.so.2", "libc.so.6", "libdl.so.2", "libgcc_s.so.1",
    "libm.so.6", "libpthread.so.0", "libresolv.so.2", "librt.so.1", "libstdc++.so.6",
    "libutil.so.1",
    # The vcpkg OpenGL port uses the host graphics stack on Linux.
    "libGL.so.1", "libGLU.so.1", "libGLX.so.0", "libOpenGL.so.0", "libEGL.so.1",
    "libX11.so.6", "libXext.so.6", "libXi.so.6", "libXmu.so.6", "libXrandr.so.2",
    "libXrender.so.1",
}
MACOS_SYSTEM_PREFIXES = ("/usr/lib/", "/System/Library/")


def run(*command, capture=False):
    return subprocess.run(
        command, check=True, text=True,
        stdout=subprocess.PIPE if capture else None,
        env={**os.environ, "LC_ALL": "C"},
    ).stdout


def digest(path):
    with path.open("rb") as stream:
        return file_digest(stream, "sha256").hexdigest()


def macos_id(path):
    names = run("otool", "-D", path, capture=True).splitlines()[1:]
    return names[0].strip() if names else None


def dependencies(path, is_macos):
    if not is_macos:
        return run("patchelf", "--print-needed", path, capture=True).splitlines()
    links = run("otool", "-L", path, capture=True).splitlines()[1:]
    names = [line.strip().split(" (compatibility version ", 1)[0] for line in links]
    if names and names[0] == macos_id(path):
        names.pop(0)
    return names


def macos_rpaths(path):
    lines = run("otool", "-l", path, capture=True).splitlines()
    return [lines[index + 2].strip().removeprefix("path ").split(" (offset ", 1)[0]
            for index, line in enumerate(lines) if line.strip() == "cmd LC_RPATH"]


def native_load_name(path, is_macos):
    if is_macos:
        name = macos_id(path)
    else:
        dynamic = run("readelf", "-d", path, capture=True)
        entry = re.search(r"\(SONAME\).*\[([^]]+)]", dynamic)
        name = entry[1] if entry else None
    return Path(name).name if name else path.name


def verify_architecture(path, is_macos):
    if is_macos:
        if run("lipo", "-archs", path, capture=True).strip() != "arm64":
            raise ValueError(f"{path} must contain exactly one arm64 Mach-O slice")
        header = run("otool", "-hv", path, capture=True)
        if not re.search(r"\bARM64\b.+\bDYLIB\b", header):
            raise ValueError(f"{path} is not an Arm64 Mach-O dynamic library")
    else:
        header = run("readelf", "-h", path, capture=True)
        if not all(re.search(pattern, header) for pattern in (
            r"Class:\s+ELF64", r"Machine:\s+Advanced Micro Devices X86-64",
            r"Type:\s+DYN",
        )):
            raise ValueError(f"{path} is not an x64 ELF shared library")


def is_system_library(name, is_macos):
    if is_macos:
        return name.startswith(MACOS_SYSTEM_PREFIXES)
    return name in LINUX_SYSTEM_LIBRARIES


def resolve_dependency(name, lib_dir, is_macos):
    if lib_dir is None:
        raise ValueError(f"Cannot resolve native dependency {name}")
    path = Path(name)
    if path.is_absolute():
        if not path.is_relative_to(lib_dir):
            raise ValueError(f"Unexpected build-host dependency {name}")
        source = path
    elif (not is_macos and path.name == name) or (
        is_macos and name in (f"@rpath/{path.name}", f"@loader_path/{path.name}")
    ):
        source = lib_dir / path.name
    else:
        raise ValueError(f"Unsupported dependency name {name}")

    if not source.is_file() or not source.resolve().is_relative_to(lib_dir):
        raise ValueError(f"Native dependency {name} is missing from {lib_dir}")
    return native_load_name(source, is_macos), source


def relocate(path, replacements, is_macos):
    if is_macos:
        arguments = []
        for original, name in replacements:
            relative = f"@loader_path/{name}"
            if original != relative:
                arguments.extend(("-change", original, relative))
        rpaths = macos_rpaths(path)
        for old_rpath in rpaths:
            if old_rpath != "@loader_path":
                arguments.extend(("-delete_rpath", old_rpath))
        if "@loader_path" not in rpaths:
            arguments.extend(("-add_rpath", "@loader_path"))
        arguments.extend(("-id", f"@rpath/{path.name}"))
        run("install_name_tool", *arguments, path)
        run("codesign", "--force", "--sign", "-", "--timestamp=none", path)
        run("codesign", "--verify", "--strict", path)
    else:
        arguments = ["--set-rpath", "$ORIGIN", "--set-soname", path.name]
        for original, name in replacements:
            if original != name:
                arguments.extend(("--replace-needed", original, name))
        run("patchelf", *arguments, path)


def stage_native(binding, destination, lib_dir, is_macos):
    destination.mkdir()
    pending = deque([(binding.name, binding)])
    packed = {}
    while pending:
        name, source = pending.popleft()
        source_hash = digest(source)
        if name in packed:
            if packed[name] != source_hash:
                raise ValueError(f"Different native libraries require the same packaged name {name}")
            continue
        packed[name] = source_hash
        verify_architecture(source, is_macos)

        replacements = []
        for dependency in dependencies(source, is_macos):
            if is_system_library(dependency, is_macos):
                continue
            dependency_name, dependency_source = resolve_dependency(dependency, lib_dir, is_macos)
            replacements.append((dependency, dependency_name))
            pending.append((dependency_name, dependency_source))

        output = destination / name
        shutil.copyfile(source, output)
        relocate(output, replacements, is_macos)

    for output in destination.iterdir():
        rpaths = macos_rpaths(output) if is_macos else run(
            "patchelf", "--print-rpath", output, capture=True).splitlines()
        if rpaths != (["@loader_path"] if is_macos else ["$ORIGIN"]):
            raise ValueError(f"{output.name} has a non-package runtime search path")
        for name in dependencies(output, is_macos):
            if is_system_library(name, is_macos):
                continue
            local_name = name.removeprefix("@loader_path/") if is_macos else name
            expected_name = f"@loader_path/{local_name}" if is_macos else local_name
            if name != expected_name or local_name not in packed:
                raise ValueError(f"{output.name} still references unpackaged native dependency {name}")

    nm_options = ("-gU",) if is_macos else ("-D", "--defined-only")
    exported = run("nm", *nm_options, destination / binding.name, capture=True)
    if not re.search(r"\b_?NativeApi_GetFunctionTable$", exported, re.MULTILINE):
        raise ValueError(f"{binding.name} does not export the generated native function table")


def copy_notice(source, destination):
    if not source.is_file() or source.stat().st_size == 0:
        raise ValueError(f"Missing native redistribution notice {source}")
    shutil.copyfile(source, destination)


def installed_port_dependencies(installation, triplet, root_port):
    status = (installation / "vcpkg" / "status").read_text(encoding="utf-8")
    dependencies_by_port = {}
    for paragraph in re.split(r"\n\s*\n", status):
        fields = dict(line.split(": ", 1) for line in paragraph.splitlines() if ": " in line)
        if fields.get("Architecture") != triplet or fields.get("Status") != "install ok installed":
            continue
        dependencies = dependencies_by_port.setdefault(fields["Package"], set())
        for name in fields.get("Depends", "").split(","):
            match = re.fullmatch(r"([a-z0-9-]+)(?::([a-z0-9-]+))?", name.strip())
            if match and match[2] in (None, triplet) and not match[1].startswith("vcpkg-"):
                dependencies.add(match[1])

    pending = [root_port]
    visited = set()
    while pending:
        name = pending.pop()
        if name in visited:
            continue
        if name not in dependencies_by_port:
            raise ValueError(f"Missing installed vcpkg port {name}:{triplet}")
        visited.add(name)
        pending.extend(dependencies_by_port[name])
    return sorted(visited)


def stage_notices(args, destination, vcpkg, triplet):
    destination.mkdir()
    port = PROVIDER_PORTS[args.provider]
    if port is None:
        smlib = args.repository_root / "externals" / "SMLib"
        copy_notice(smlib / "LICENSE", destination / "SMLib-LICENSE.txt")
        copy_notice(smlib / "THIRD_PARTY_NOTICES.md", destination / "SMLib-THIRD-PARTY-NOTICES.txt")
        return
    installation = vcpkg / "installed"
    for name in installed_port_dependencies(installation, triplet, port):
        # These vcpkg ports install no third-party headers or libraries.
        if name not in {"opengl", "boost-uninstall"}:
            copy_notice(installation / triplet / "share" / name / "copyright",
                        destination / f"{name}.txt")


def build(args):
    system, machine, triplet, suffix = TARGETS[args.runtime_identifier]
    if platform.system() != system or platform.machine() != machine:
        raise ValueError(f"Build {args.runtime_identifier} on a native {system} {machine} host")
    is_macos = system == "Darwin"
    vcpkg_root = os.environ.get("VCPKG_ROOT")
    vcpkg = None
    if PROVIDER_PORTS[args.provider]:
        if not vcpkg_root:
            raise ValueError("VCPKG_ROOT is required for this provider")
        vcpkg = Path(vcpkg_root).resolve(strict=True)
    output = args.output_root
    output.mkdir(parents=True, exist_ok=True)
    library_dir = output / "native-build" / "lib"
    generate = [
        os.environ.get("DOTNET_HOST_PATH") or "dotnet",
        args.generator, "--provider", args.provider, "--repository-root", args.repository_root,
        "--output-root", output, "--configuration", args.configuration,
        "--runtime-identifier", args.runtime_identifier,
    ]
    configure = [
        "cmake", "-S", output / "cpp", "-B", output / "native-build", "-G", "Ninja",
        "-DCMAKE_CXX_COMPILER=clang++", f"-DCMAKE_BUILD_TYPE={args.configuration}",
        f"-DCMAKE_LIBRARY_OUTPUT_DIRECTORY={library_dir}",
    ]
    if is_macos:
        configure.extend(("-DCMAKE_OSX_ARCHITECTURES=arm64", "-DCMAKE_OSX_DEPLOYMENT_TARGET=15.0",
                          "-DCMAKE_SHARED_LINKER_FLAGS=-Wl,-headerpad_max_install_names"))
    else:
        configure.append("-DCMAKE_SHARED_LINKER_FLAGS=-Wl,-z,defs")
    lib_dir = None
    if vcpkg:
        configuration_dir = ("debug", "lib") if args.configuration == "Debug" else ("lib",)
        lib_dir = (vcpkg / "installed" / triplet).joinpath(*configuration_dir).resolve(strict=True)
        generate.extend(("--vcpkg-root", vcpkg))
        configure.extend((
            f"-DCMAKE_TOOLCHAIN_FILE={vcpkg / 'scripts' / 'buildsystems' / 'vcpkg.cmake'}",
            f"-DVCPKG_TARGET_TRIPLET={triplet}",
            "-DVCPKG_MANIFEST_MODE=OFF",
        ))

    run(*generate)
    run(*configure)
    run("cmake", "--build", output / "native-build", "--config", args.configuration)
    if not any((output / "csharp").rglob("*.cs")):
        raise ValueError("The provider generated no C# sources")

    with tempfile.TemporaryDirectory(prefix="stage-", dir=output) as temporary:
        stage = Path(temporary) / "package"
        stage.mkdir()
        binding = library_dir / f"lib{args.library_name}{suffix}"
        stage_native(binding, stage / "native", lib_dir, is_macos)
        stage_notices(args, stage / "notices", vcpkg, triplet)
        package = output / "package"
        if package.is_dir():
            shutil.rmtree(package)
        stage.rename(package)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--provider", choices=PROVIDER_PORTS, required=True)
    parser.add_argument("--runtime-identifier", choices=TARGETS, required=True)
    parser.add_argument("--library-name", required=True)
    parser.add_argument("--configuration", required=True)
    for option in ("repository-root", "output-root", "generator"):
        parser.add_argument(f"--{option}", type=Path, required=True)
    args = parser.parse_args()
    try:
        build(args)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print(f"Unix binding build failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
