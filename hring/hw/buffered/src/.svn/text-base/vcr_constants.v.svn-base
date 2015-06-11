// $Id$

/*
Copyright (c) 2007-2009, Trustees of The Leland Stanford Junior University
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

Redistributions of source code must retain the above copyright notice, this list
of conditions and the following disclaimer.
Redistributions in binary form must reproduce the above copyright notice, this 
list of conditions and the following disclaimer in the documentation and/or 
other materials provided with the distribution.
Neither the name of the Stanford University nor the names of its contributors 
may be used to endorse or promote products derived from this software without 
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR 
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON 
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

// global constant definitions

`ifndef _VCR_CONSTANTS_V_
`define _VCR_CONSTANTS_V_

`include "c_constants.v"

//------------------------------------------------------------------------------
// credit handling implementation variants
//------------------------------------------------------------------------------

// indicate credit level to input side
`define INT_FLOW_CTRL_TYPE_LEVEL 0

// push credits to input side one by one
`define INT_FLOW_CTRL_TYPE_PUSH  1

`define INT_FLOW_CTRL_TYPE_LAST `INT_FLOW_CTRL_TYPE_PUSH


//------------------------------------------------------------------------------
// VC allocator implementation variants
//------------------------------------------------------------------------------

// separable, input-first
`define VC_ALLOC_TYPE_SEP_IF    0

// separable, output-first
`define VC_ALLOC_TYPE_SEP_OF    1

// wavefront-based
// (note: add WF_ALLOC_TYPE_* constant to select wavefront variant)
`define VC_ALLOC_TYPE_WF_BASE   2
`define VC_ALLOC_TYPE_WF_LIMIT  (`VC_ALLOC_TYPE_WF_BASE + `WF_ALLOC_TYPE_LAST)

`define VC_ALLOC_TYPE_LAST `VC_ALLOC_TYPE_WF_LIMIT


//------------------------------------------------------------------------------
// switch allocator implementation variants
//------------------------------------------------------------------------------

// separable, input-first
`define SW_ALLOC_TYPE_SEP_IF   0

// separable, output-first
`define SW_ALLOC_TYPE_SEP_OF   1

// wavefront-based
// (note: add WF_ALLOC_TYPE_* constant to select wavefront variant)
`define SW_ALLOC_TYPE_WF_BASE  2
`define SW_ALLOC_TYPE_WF_LIMIT (`SW_ALLOC_TYPE_WF_BASE + `WF_ALLOC_TYPE_LAST)

`define SW_ALLOC_TYPE_LAST `SW_ALLOC_TYPE_WF_LIMIT


//------------------------------------------------------------------------------
// speculation types for switch allocator
//------------------------------------------------------------------------------

// disable speculative switch allocation
`define SW_ALLOC_SPEC_TYPE_NONE           0

// use non-speculative requests to mask non-speculative grants
`define SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS 1

// use non-speculative requests to mask speculative requests
`define SW_ALLOC_SPEC_TYPE_REQS_MASK_REQS 2

// use non-speculative grants to mask non-speculative grants
`define SW_ALLOC_SPEC_TYPE_GNTS_MASK_GNTS 3

// use non-speculative grants to mask speculative requests
`define SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS 4

`define SW_ALLOC_SPEC_TYPE_LAST `SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS


`endif
