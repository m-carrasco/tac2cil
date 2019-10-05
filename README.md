# tac2cil
# Intro

**analysis-net** is a [framework](https://github.com/edgardozoppi/analysis-net) that focuses on static analysis of .NET programs.  It is generates a [three-address code](https://en.wikipedia.org/wiki/Three-address_code) representation for the .NET bytecode ([CIL](https://en.wikipedia.org/wiki/Common_Intermediate_Language)) and its analyses are implemented on this type of instructions.  CIL is a stack-based bytecode while **analysis-net**'s code representation is register-based.

**tac2cil** complements **analysis-net** by providing the ability to compile a .NET program from the three-address code representation. This feature unlocks the possiblity to implement instrumentations/optimizations on .NET programs but from a much nicer abstraction (three-address code).

![](/images/flow.svg)

# Status

|master| [![Build Status](https://travis-ci.com/m7nu3l/tac2cil.svg?token=f7qzBQCoptr4sx6YDGWa&branch=master)](https://travis-ci.com/m7nu3l/tac2cil) |
|--|--|
