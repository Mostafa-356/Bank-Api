# Changelog

## 1.0.0 (2026-09-18)


### ⚠ BREAKING CHANGES

* All sensitive values now use {PLACEHOLDER} syntax

### Security

* Complete remediation of hard-coded credentials in configuration ([4caff25](https://github.com/Mostafa-356/Bank-Api/commit/4caff25789c0dfccfb8aee0443ec2ac104e8eb7f))


### Features

* Add AutoMapper with domain-based mapping profiles ([6bbb913](https://github.com/Mostafa-356/Bank-Api/commit/6bbb913b2ce2757a7940b305bbe24c68bef1a4a9))
* add CQRS Commands and Queries structure for Deposit domain ([6966758](https://github.com/Mostafa-356/Bank-Api/commit/6966758b9c063e01f9da0eef384afd92bde5acad))
* Add project reorganization spec and restructure project ([a4c4ab0](https://github.com/Mostafa-356/Bank-Api/commit/a4c4ab0063f66eda23ce6944995ce2842c127967))
* Complete DTO reorganization and namespace restructuring ([9cfe655](https://github.com/Mostafa-356/Bank-Api/commit/9cfe655017b3034e1939f6e38a84899c863b46de))
* create centralized Templates folder with notification and document templates ([8220b13](https://github.com/Mostafa-356/Bank-Api/commit/8220b13b504b919ae94b55cc44702d44c4cd4b12))
* implement core domain entities, DTOs, mapping profiles, and service interfaces for banking operations. ([17e5ffc](https://github.com/Mostafa-356/Bank-Api/commit/17e5ffc3ab624c449ea8cff800c7e3050b4215d9))
* implement Register, Profile, Logs, and Docs pages with persistent branding and Dashboard fix ([5540f57](https://github.com/Mostafa-356/Bank-Api/commit/5540f57c0fe307a7a52f12e11332d75845d108a0))
* migrate from SQLite to SQL Server with complete schema and documentation ([e89c4d8](https://github.com/Mostafa-356/Bank-Api/commit/e89c4d84ee4b05b25a26a40dd4d18699d678e1b2))
* Neon database migration - Phases 1-6 implementation ([1dcb761](https://github.com/Mostafa-356/Bank-Api/commit/1dcb7613093849964c5b475edde77a8b0388e17c))
* restructure backend and implement payments integration ([c0a6e16](https://github.com/Mostafa-356/Bank-Api/commit/c0a6e16eebf13eca3bad3894c2991727fd6c194d))
* setup automated releases with release-please ([9389807](https://github.com/Mostafa-356/Bank-Api/commit/938980739a8287c1c1c9fbc07afe48772ac92ec9))


### Bug Fixes

* Add JsonRequired attribute to value type properties in request records ([edf4e38](https://github.com/Mostafa-356/Bank-Api/commit/edf4e387e4519e2d2eebf1e74d55f302dd028827))
* add NuGet configuration files and deployment scripts to resolve package resolution errors ([6a8fe4b](https://github.com/Mostafa-356/Bank-Api/commit/6a8fe4b0585b699c85e14092d487d66c3b77e890))
* add production Docker build and comprehensive deployment solutions ([cff30ff](https://github.com/Mostafa-356/Bank-Api/commit/cff30ff7249593be2746e39249a779885ac57461))
* Auth & Account Controllers - Remove duplicates, standardize null-safety, add missing endpoints ([5caa93a](https://github.com/Mostafa-356/Bank-Api/commit/5caa93ad65e03a6852bf19442de13b5e78787c2d))
* Implement IDisposable properly and add assertions to test ([af587d2](https://github.com/Mostafa-356/Bank-Api/commit/af587d2739917ba7f4a0a08be8a66962dfb1257e))
* make GetRequestBodyAsync static and check ReadAsync return value for proper buffer handling ([7924d62](https://github.com/Mostafa-356/Bank-Api/commit/7924d62fd58dc1c7682f36edda37be05bb9f9b3a))
* pass token to release-please-action to allow PR creation ([99f4806](https://github.com/Mostafa-356/Bank-Api/commit/99f480639146946415969ca8a6b3ec77171335b7))
* Register DepositProductManagementService with correct interface IDepositProductService ([255d542](https://github.com/Mostafa-356/Bank-Api/commit/255d5422652af5bcca0ea6a31bd5bd78eca22956))
* Remove infinite recursion from GetCurrentUserId and GetCurrentSessionToken methods ([9c8fd34](https://github.com/Mostafa-356/Bank-Api/commit/9c8fd3445511cb2d52bc29c07232e01f6bbdbe13))
* remove unused _configuration field from SmsService ([59e5793](https://github.com/Mostafa-356/Bank-Api/commit/59e5793a5dcd41372bf8b5ac3a939f5511797da6))
* remove unused private field '_notificationService' from DepositMaturityService ([3a1271d](https://github.com/Mostafa-356/Bank-Api/commit/3a1271d9b6b8677280f400738ca07b607d662b9e))
* remove unused private field '_sanitizer' from capture classes ([71ce2cf](https://github.com/Mostafa-356/Bank-Api/commit/71ce2cf518fd1e54f3e0e1cd6978d580387463ea))
* Rename parameters to match base class declarations for consistency ([52b6490](https://github.com/Mostafa-356/Bank-Api/commit/52b64904d77036adaebec736ec42a2d4eff9176c))
* resolve all identified runtime and configuration errors ([7b5cd75](https://github.com/Mostafa-356/Bank-Api/commit/7b5cd75125cb0031237dde4014a25679a9f33ffa))
* resolve async/await, parameter reduction, and logging issues ([a2abe2d](https://github.com/Mostafa-356/Bank-Api/commit/a2abe2dfc29d57eb53e61b924736e08133f75e8d))
* resolve CodeQL high severity alerts for clear text storage of sensitive info ([0a4a276](https://github.com/Mostafa-356/Bank-Api/commit/0a4a2762c10cd0609b43289a380fcd20385110af))
* resolve critical build error and cleanup root directory ([f2a94cd](https://github.com/Mostafa-356/Bank-Api/commit/f2a94cdedf0690dfb5c52175bb9d7fb0786c6b27))
* resolve dotnet command not found errors in Docker container ([b56219f](https://github.com/Mostafa-356/Bank-Api/commit/b56219f7bb1e05d4c9e180c540953c22cd61e11f))
* resolve high-priority SonarQube issues - remove unused field, make static class, add constructor, fix JS globals ([cc33fc3](https://github.com/Mostafa-356/Bank-Api/commit/cc33fc395052cf9c2ff608b1da7ff5881b46bfa7))
* sanitize user input before logging to resolve CodeQL log injection alerts (CWE-117) ([bc918d4](https://github.com/Mostafa-356/Bank-Api/commit/bc918d494288f165f9d2fa186f1ccb04d2831733))
* simplify Docker build for Railpack compatibility ([56bf52b](https://github.com/Mostafa-356/Bank-Api/commit/56bf52b1972c09f7d80980809228a3c8ef357eed))
* update GitHub Actions to use version tags instead of commit SHAs for security ([2a263a5](https://github.com/Mostafa-356/Bank-Api/commit/2a263a511bf40d30846cbe6b03dd70fc28761feb))
* Use async methods and fix logging string interpolation ([fff9381](https://github.com/Mostafa-356/Bank-Api/commit/fff9381923ef239bf3d56ea398d94ad5596fb92a))
