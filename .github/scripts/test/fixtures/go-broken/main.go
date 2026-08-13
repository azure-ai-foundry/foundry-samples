package main

import "fmt"

func main() {
	// Deliberate compile error: undefinedVar is not declared.
	fmt.Println(undefinedVar)
}
