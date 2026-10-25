cmake_minimum_required(VERSION 3.16)

# Consumes the installed package the way a downstream project does - find_package() against an install prefix,
# not add_subdirectory() against this repo. Its own project, so a config file that only works inside the build
# tree fails here.
project(fixedwidth_package_consumer LANGUAGES CXX)

find_package(fixedwidth REQUIRED)

add_executable(package_consumer main.cpp)
target_link_libraries(package_consumer PRIVATE fw::fixedwidth)
fixedwidth_copy_native_library(package_consumer)

enable_testing()
add_test(NAME package_consumer COMMAND package_consumer)
set_tests_properties(package_consumer PROPERTIES PASS_REGULAR_EXPRESSION "package ok")
