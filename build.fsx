#if run_with_bin_sh
  # See why this works at http://stackoverflow.com/a/21948918/637783
  exec fsharpi --define:mono_posix --exec $0 $*
#endif

#if FSharp_MakeFile
(* Make File *)

#I "packages/FAKE/tools"
#r "FakeLib.dll"

open Fake

let projFiles =
  !! "./Src/**/GoogleApis.Core.csproj" //Core First
  ++ "./Src/**/GoogleApis.csproj"  //Then APIS
  ++ "./Src/**/GoogleApis.Auth.csproj" //Then auth
  ++ "./Src/**/*.csproj" //Then Everything else
  |> (fun incl -> if isMono then
                    incl //Remove WinRT and Phone from Mono Builds
                    -- "./Src/**/*.WinRT.csproj"
                    -- "./Src/**/*.WP.csproj"
                  else
                    incl)

Target "NugetRestore" (fun () ->
    trace " --- Nuget Restore --- "
    !! "./Src/**/packages.config"
    |> Seq.iter (RestorePackage id)
)

Target "Clean" (fun () ->
    trace " --- Cleaning stuff --- "
    let allBins = !! "Src/**/bin/" ++ "Src/**/obj/"
    CleanDirs allBins
)

Target "Build" (fun () ->
    trace " --- Building the libs --- "

    let buildMode = getBuildParamOrDefault "buildMode" "Release"
    let setParams defaults =
            { defaults with
                Verbosity = Some(Quiet)
                Targets = ["Build"]
                Properties =
                    [
                        "Optimize", "True"
                        "DebugSymbols", "True"
                        "Configuration", buildMode
                    ]
             }

    projFiles
      |> Seq.iter (build setParams)
)

Target "Test" (fun () ->
    trace " --- Test the libs --- "
    !! "./Src/**/*Apis.Tests/bin/**/*Apis.Tests.dll"
    ++ "./Src/**/*Auth.Tests/bin/**/*Auth.Tests.dll"
    |> NUnit (fun p -> { p with ToolPath = "./packages/NUnit.Runners/tools/" })
)

"NugetRestore"
  ==> "Build"
  ==> "Test"

RunTargetOrDefault "Test"


#else

(*
 * Crossplatform FSharp Makefile Bootstrapper
 * Apache licensed - Copyright 2014 Jay Tuley <jay+code@tuley.name>
 * v 2.0 https://gist.github.com/jbtule/9243729
 *
 * How to use:
 *  On Windows `fsi --exec build.fsx <buildtarget>
 *    *Note:* if you have trouble first run "%vs120comntools%\vsvars32.bat" or use the "Developer Command Prompt for VS201X"
 *                                                           or install https://github.com/Iristyle/Posh-VsVars#posh-vsvars
 *
 *  On Mac Or Linux `./build.fsx <buildtarget>`
 *    *Note:* But if you have trouble then use `sh build.fsx <buildtarget>`
 *
 *)

open System
open System.IO
open System.Diagnostics

(* helper functions *)
#if mono_posix
#r "Mono.Posix.dll"
open Mono.Unix.Native
let applyExecutionPermissionUnix path =
    let _,stat = Syscall.lstat(path)
    Syscall.chmod(path, FilePermissions.S_IXUSR ||| stat.st_mode) |> ignore
#else
let applyExecutionPermissionUnix path = ()
#endif

let doesNotExist path =
    path |> Path.GetFullPath |> File.Exists |> not

let execAt (workingDir:string) (exePath:string) (args:string seq) =
    let processStart (psi:ProcessStartInfo) =
        let ps = Process.Start(psi)
        ps.WaitForExit ()
        ps.ExitCode
    let fullExePath = exePath |> Path.GetFullPath
    applyExecutionPermissionUnix fullExePath
    let exitCode = ProcessStartInfo(
                        fullExePath,
                        args |> String.concat " ",
                        WorkingDirectory = (workingDir |> Path.GetFullPath),
                        UseShellExecute = false)
                   |> processStart
    if exitCode <> 0 then
        exit exitCode
    ()

let exec = execAt Environment.CurrentDirectory

let downloadNugetTo path =
    let fullPath = path |> Path.GetFullPath;
    if doesNotExist fullPath then
        printf "Downloading NuGet..."
        use webClient = new System.Net.WebClient()
        fullPath |> Path.GetDirectoryName |> Directory.CreateDirectory |> ignore
        webClient.DownloadFile("https://nuget.org/nuget.exe", path |> Path.GetFullPath)
        printfn "Done."

let passedArgs = fsi.CommandLineArgs.[1..] |> Array.toList

(* execution script customize below *)

let makeFsx = fsi.CommandLineArgs.[0]

let nugetExe = ".nuget/NuGet.exe"
let fakeExe = "packages/FAKE/tools/FAKE.exe"
let nunitExe = "packages/NUnit.Runners/tools/Nunit/nunit-console.exe"


downloadNugetTo nugetExe

if doesNotExist fakeExe then
    exec nugetExe ["install"; "fake"; "-OutputDirectory packages"; "-ExcludeVersion"]
if doesNotExist nunitExe then
    exec nugetExe ["install"; "NUnit.Runners"; "-OutputDirectory packages"; "-ExcludeVersion"]
exec fakeExe ([makeFsx; "-d:FSharp_MakeFile"] @ passedArgs)

#endif
