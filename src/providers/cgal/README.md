# CGAL provider boundary

This directory reserves the concrete `cgal` provider identity so repository dependency and source
neutrality checks cover it before downstream CGAL projects are delivered. The approved target
contains real Generator, Runtime, and Windows projects; their implementation is intentionally owned
by the separate CGAL delivery change.
