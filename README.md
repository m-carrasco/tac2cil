# tac2cil
# Intro

**analysis-net** is a [framework](https://github.com/edgardozoppi/analysis-net) that focuses on static analysis of .NET programs.  It generates a [three-address code](https://en.wikipedia.org/wiki/Three-address_code) representation for the .NET bytecode ([CIL](https://en.wikipedia.org/wiki/Common_Intermediate_Language)) and its analyses are implemented on this type of instructions.  CIL is a stack-based bytecode while **analysis-net**'s code representation is register-based.

**tac2cil** complements **analysis-net** by providing the ability to compile a .NET program from the three-address code representation. This feature unlocks the possiblity to implement instrumentations/optimizations on .NET programs but from a much nicer abstraction (three-address code).

<p align="center">
<img src="/images/flow.svg">
</p>

# Components

## CodeProvider

A *CodeProvider* loads a .NET binary and generate the code model of **analysis-net**. The code model is a set of classes that model entities of the metadata. In the code model, you can find classes modeling types, methods, and instructions. CIL instructions are translated to simplified bytecode instructions (SIL). SIL is an **analysis-net**'s set of stack-based instructions and is considerably smaller than CIL. 

We developed a *CodeProvider* that uses Cecil to load the .NET binary and from there we create the **analysis-net**'s code model. There are two other *CodeProviders* implemented in **analysis-net**. 

## CodeGenerator

A *CodeGenerator* takes an **analysis-net**'s code model instance with SIL instructions and generates a semantically equivalent .NET binary. We decided to implement a *CodeGenerator* backended by Cecil. In particular, this *CodeGenerator* takes analysis-net's code model and generates an equivalent model in Cecil. Then, Cecil generates the assembly.

# Status

|master| [![Build Status](https://travis-ci.com/m7nu3l/tac2cil.svg?token=f7qzBQCoptr4sx6YDGWa&branch=master)](https://travis-ci.com/m7nu3l/tac2cil) |
|--|--|
